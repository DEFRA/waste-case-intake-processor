using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using WasteCaseIntakeProcessor.WasteObligations;

namespace WasteCaseIntakeProcessor.Test.WasteObligations;

public class ServiceCollectionExtensionsTests
{
    private const string EmptyPage = """
        { "complianceDeclarations": [], "total": 0, "page": 1, "pageSize": 100 }
        """;

    private static readonly string s_clientSecret = Guid.NewGuid().ToString("N");

    private static readonly ComplianceDeclarationSearch s_firstPage = new() { Page = 1, PageSize = 100 };

    [Fact]
    public async Task Application_when_waste_obligations_not_configured_starts_and_client_fails_only_when_called()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory(new Dictionary<string, string?>());
        using var http = factory.CreateClient();

        var health = await http.GetAsync("/health", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IWasteObligationsClient>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SearchComplianceDeclarations(s_firstPage, cancellationToken)
        );
    }

    [Fact]
    public async Task Application_when_configured_client_works_outside_an_http_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        await using var factory = CreateFactory(Configured("http://localhost:8090/"), handler);

        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IWasteObligationsClient>();
        var page = await client.SearchComplianceDeclarations(s_firstPage, cancellationToken);

        Assert.Equal(0, page.Total);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("http://localhost:8090/compliance-declarations?page=1&pageSize=100"), request.RequestUri);
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_base_address_has_path_keeps_it()
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = BuildClient(Configured("https://waste-obligations.test/api"), handler);

        await client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://waste-obligations.test/api/compliance-declarations?page=1&pageSize=100"),
            Assert.Single(handler.Requests).RequestUri
        );
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_server_error_retries_then_succeeds()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.ServiceUnavailable)
            .RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = BuildClient(Configured("https://waste-obligations.test/"), handler);

        var page = await client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken);

        Assert.Equal(0, page.Total);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_connection_fails_retries_then_succeeds()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = BuildClient(Configured("https://waste-obligations.test/"), handler);

        var page = await client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken);

        Assert.Equal(0, page.Total);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_response_body_breaks_retries_then_succeeds()
    {
        var handler = new StubHttpMessageHandler().RespondWithBrokenBody().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = BuildClient(Configured("https://waste-obligations.test/"), handler);

        var page = await client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken);

        Assert.Equal(0, page.Total);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_response_body_stalls_retries_after_attempt_timeout()
    {
        var settings = Configured("https://waste-obligations.test/");
        settings["WasteObligations:TotalRequestTimeout:Timeout"] = "00:00:10";
        settings["WasteObligations:AttemptTimeout:Timeout"] = "00:00:00.500";
        var handler = new StubHttpMessageHandler().RespondWithStalledBody().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = BuildClient(settings, handler);

        var page = await client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken);

        Assert.Equal(0, page.Total);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AddWasteObligationsClient_when_response_body_always_stalls_gives_up_after_total_timeout()
    {
        var settings = Configured("https://waste-obligations.test/");
        settings["WasteObligations:TotalRequestTimeout:Timeout"] = "00:00:01";
        settings["WasteObligations:AttemptTimeout:Timeout"] = "00:00:00.500";
        var handler = new StubHttpMessageHandler()
            .RespondWithStalledBody()
            .RespondWithStalledBody()
            .RespondWithStalledBody()
            .RespondWithStalledBody();
        var client = BuildClient(settings, handler);
        using var testDeadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        testDeadline.CancelAfter(TimeSpan.FromSeconds(30));

        var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            client.SearchComplianceDeclarations(s_firstPage, testDeadline.Token)
        );

        // Before the retries run out (4 attempts) and well under the HttpClient.Timeout backstop (total + 5 s),
        // so the total timeout ended the call.
        Assert.True(handler.Requests.Count < 4, $"Sent {handler.Requests.Count}");
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(4), $"Took {elapsed.Elapsed}");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AddWasteObligationsClient_when_not_authorised_does_not_retry(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler().RespondWith(statusCode);
        var client = BuildClient(Configured("https://waste-obligations.test/"), handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchComplianceDeclarations(s_firstPage, TestContext.Current.CancellationToken)
        );

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    private static Dictionary<string, string?> Configured(string baseAddress) =>
        new()
        {
            ["WasteObligations:BaseAddress"] = baseAddress,
            ["WasteObligations:ClientId"] = "case-intake-processor",
            ["WasteObligations:ClientSecret"] = s_clientSecret,
            ["WasteObligations:Retry:Delay"] = "00:00:00.001",
            ["WasteObligations:Retry:UseJitter"] = "false",
        };

    private static IWasteObligationsClient BuildClient(
        Dictionary<string, string?> settings,
        StubHttpMessageHandler handler
    )
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWasteObligationsClient(configuration);
        services
            .AddHttpClient<IWasteObligationsClient, WasteObligationsClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<IWasteObligationsClient>();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Dictionary<string, string?> settings,
        StubHttpMessageHandler? handler = null
    ) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AWS:ServiceURL", "http://localhost:4566");
            builder.UseSetting("AWS:Region", "eu-west-2");
            builder.UseSetting("IntakeEventConsumer:ProcessingEnabled", "false");
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);

            if (handler is not null)
                builder.ConfigureTestServices(services =>
                    services
                        .AddHttpClient<IWasteObligationsClient, WasteObligationsClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => handler)
                );
        });
}
