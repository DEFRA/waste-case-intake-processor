using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WasteCaseIntakeProcessor.WasteObligations;

namespace WasteCaseIntakeProcessor.Test.WasteObligations;

public class WasteObligationsClientTests
{
    private const string BaseAddress = "https://waste-obligations.test/";

    private static readonly string s_password = Guid.NewGuid().ToString("N");

    private const string EmptyPage = """
        { "complianceDeclarations": [], "total": 0, "page": 1, "pageSize": 100 }
        """;

    private const string DeclarationsPage = """
        {
          "complianceDeclarations": [
            {
              "id": "68d3f0c2a1b2c3d4e5f60718",
              "version": 3,
              "created": "2026-02-10T09:15:00+00:00",
              "updated": "2026-03-01T14:30:00+00:00",
              "status": "Cancelled",
              "organisation": {
                "id": "6f1c2a3b-4d5e-4f60-8a9b-0c1d2e3f4a5b",
                "registrationType": "DirectProducer",
                "name": "Example Packaging Ltd",
                "schemeOperatorName": "Example Scheme Operator Ltd",
                "referenceNumber": "100123",
                "regulator": "Environment Agency",
                "regulatorEmail": "regulator@example.test"
              },
              "obligationYear": 2026,
              "obligations": [{ "material": "Glass" }],
              "obligationStatus": "Met",
              "submitterName": "Sam Submitter",
              "isRegulation43Compliant": true,
              "obligationCoveragePercentage": 100,
              "audit": [
                {
                  "user": { "id": "0b8a7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d", "email": "sam@example.test", "name": "Sam Submitter" },
                  "timestamp": "2026-02-10T09:15:00+00:00",
                  "action": "Submitted"
                },
                {
                  "user": { "id": "1c9b8d7e-6f5a-4b4c-8d3e-2f1a0b9c8d7e", "email": "alex@example.test", "name": "Alex Regulator" },
                  "timestamp": "2026-02-20T11:00:00+00:00",
                  "action": "Accepted"
                },
                {
                  "user": { "id": "2d0c9e8f-7a6b-4c5d-9e4f-3a2b1c0d9e8f", "email": "kim@example.test", "name": "Kim Regulator" },
                  "timestamp": "2026-03-01T14:30:00+00:00",
                  "action": "Cancelled",
                  "reason": "Recycling obligations changed"
                }
              ]
            }
          ],
          "total": 241,
          "page": 2,
          "pageSize": 100
        }
        """;

    [Fact]
    public async Task SearchComplianceDeclarations_when_no_filter_sends_page_and_page_size_only()
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = CreateClient(handler);

        await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch { Page = 3, PageSize = 100 },
            TestContext.Current.CancellationToken
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(BaseAddress + "compliance-declarations?page=3&pageSize=100"), request.RequestUri);
    }

    [Fact]
    public async Task SearchComplianceDeclarations_when_filters_set_sends_waste_obligations_values()
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = CreateClient(handler);

        await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch
            {
                Page = 1,
                PageSize = 1,
                Status = ComplianceDeclarationStatus.Cancelled,
                ObligationYear = 2026,
                RegistrationType = RegistrationType.ComplianceScheme,
                Country = BusinessCountry.NorthernIreland,
            },
            TestContext.Current.CancellationToken
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "?page=1&pageSize=1&status=Cancelled&obligationYear=2026&registrationType=ComplianceScheme&country=GB-NIR",
            request.RequestUri!.Query
        );
    }

    [Theory]
    [InlineData(BusinessCountry.England, "GB-ENG")]
    [InlineData(BusinessCountry.NorthernIreland, "GB-NIR")]
    [InlineData(BusinessCountry.Scotland, "GB-SCT")]
    [InlineData(BusinessCountry.Wales, "GB-WLS")]
    public async Task SearchComplianceDeclarations_when_country_set_sends_country_code(
        BusinessCountry country,
        string expectedCode
    )
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = CreateClient(handler);

        await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch
            {
                Page = 1,
                PageSize = 1,
                Country = country,
            },
            TestContext.Current.CancellationToken
        );

        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("&country=" + expectedCode, request.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchComplianceDeclarations_when_called_sends_basic_authorization()
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, EmptyPage);
        var client = CreateClient(handler);

        await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
            TestContext.Current.CancellationToken
        );

        var authorization = Assert.Single(handler.Requests).Headers.Authorization;
        Assert.NotNull(authorization);
        Assert.Equal("Basic", authorization.Scheme);
        Assert.Equal(
            "case-intake-processor:" + s_password,
            Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter!))
        );
    }

    [Fact]
    public async Task SearchComplianceDeclarations_when_page_returned_parses_declarations_and_audit()
    {
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, DeclarationsPage);
        var client = CreateClient(handler);

        var page = await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch { Page = 2, PageSize = 100 },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(241, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Equal(100, page.PageSize);

        var declaration = Assert.Single(page.ComplianceDeclarations);
        Assert.Equal("68d3f0c2a1b2c3d4e5f60718", declaration.Id);
        Assert.Equal(3, declaration.Version);
        Assert.Equal(new DateTimeOffset(2026, 2, 10, 9, 15, 0, TimeSpan.Zero), declaration.Created);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 14, 30, 0, TimeSpan.Zero), declaration.Updated);
        Assert.Equal("Cancelled", declaration.Status);
        Assert.Equal(2026, declaration.ObligationYear);
        Assert.Equal("Sam Submitter", declaration.SubmitterName);

        Assert.Equal(Guid.Parse("6f1c2a3b-4d5e-4f60-8a9b-0c1d2e3f4a5b"), declaration.Organisation!.Id);
        Assert.Equal("DirectProducer", declaration.Organisation.RegistrationType);
        Assert.Equal("Example Packaging Ltd", declaration.Organisation.Name);
        Assert.Equal("Example Scheme Operator Ltd", declaration.Organisation.SchemeOperatorName);
        Assert.Equal("100123", declaration.Organisation.ReferenceNumber);
        Assert.Equal("Environment Agency", declaration.Organisation.Regulator);
        Assert.Null(declaration.Organisation.BusinessCountry);

        Assert.Collection(
            declaration.Audit!,
            submitted =>
            {
                Assert.Equal("Submitted", submitted.Action);
                Assert.Null(submitted.Reason);
            },
            accepted =>
            {
                Assert.Equal("Accepted", accepted.Action);
                Assert.Equal(new DateTimeOffset(2026, 2, 20, 11, 0, 0, TimeSpan.Zero), accepted.Timestamp);
                Assert.Equal("1c9b8d7e-6f5a-4b4c-8d3e-2f1a0b9c8d7e", accepted.User!.Id);
                Assert.Equal("alex@example.test", accepted.User.Email);
                Assert.Equal("Alex Regulator", accepted.User.Name);
            },
            cancelled =>
            {
                Assert.Equal("Cancelled", cancelled.Action);
                Assert.Equal("Recycling obligations changed", cancelled.Reason);
            }
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SearchComplianceDeclarations_when_response_not_successful_throws_with_status_and_without_body(
        HttpStatusCode statusCode
    )
    {
        const string body = """{ "detail": "private downstream detail" }""";
        var handler = new StubHttpMessageHandler().RespondWith(statusCode, body);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchComplianceDeclarations(
                new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.DoesNotContain("private downstream detail", exception.Message);
    }

    [Theory]
    [InlineData("\"id\": \"68d3f0c2a1b2c3d4e5f60718\",", "")]
    [InlineData("\"id\": \"68d3f0c2a1b2c3d4e5f60718\",", "\"id\": null,")]
    [InlineData("\"version\": 3,", "")]
    [InlineData("\"version\": 3,", "\"version\": \"three\",")]
    [InlineData("\"created\": \"2026-02-10T09:15:00+00:00\",", "\"created\": \"not a date\",")]
    [InlineData("\"total\": 241,", "")]
    public async Task SearchComplianceDeclarations_when_row_key_missing_or_value_malformed_throws(
        string original,
        string replacement
    )
    {
        var client = CreateClient(RespondWithSampleWhere(original, replacement));

        await Assert.ThrowsAsync<JsonException>(() =>
            client.SearchComplianceDeclarations(
                new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData("\"organisation\": {", "\"organisation\": null, \"ignored\": {")]
    [InlineData("\"regulator\": \"Environment Agency\",", "")]
    [InlineData("\"regulator\": \"Environment Agency\",", "\"regulator\": null,")]
    [InlineData("\"created\": \"2026-02-10T09:15:00+00:00\",", "")]
    [InlineData("\"action\": \"Accepted\"", "\"action\": null")]
    [InlineData("\"email\": \"alex@example.test\", ", "")]
    [InlineData("\"submitterName\": \"Sam Submitter\",", "")]
    public async Task SearchComplianceDeclarations_when_other_field_missing_or_null_still_returns_row(
        string original,
        string replacement
    )
    {
        var client = CreateClient(RespondWithSampleWhere(original, replacement));

        var page = await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
            TestContext.Current.CancellationToken
        );

        var declaration = Assert.Single(page.ComplianceDeclarations);
        Assert.Equal("68d3f0c2a1b2c3d4e5f60718", declaration.Id);
        Assert.Equal(3, declaration.Version);
    }

    [Fact]
    public async Task SearchComplianceDeclarations_when_regulator_missing_returns_null_regulator()
    {
        var client = CreateClient(RespondWithSampleWhere("\"regulator\": \"Environment Agency\",", ""));

        var page = await client.SearchComplianceDeclarations(
            new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
            TestContext.Current.CancellationToken
        );

        Assert.Null(Assert.Single(page.ComplianceDeclarations).Organisation!.Regulator);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task SearchComplianceDeclarations_when_paging_out_of_range_throws_before_sending(
        int page,
        int pageSize
    )
    {
        var handler = new StubHttpMessageHandler();
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchComplianceDeclarations(
                new ComplianceDeclarationSearch { Page = page, PageSize = pageSize },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("set-automatically-when-deployed", "case-intake-processor", "secret-value")]
    [InlineData("http://waste-obligations.test/", "case-intake-processor", "secret-value")]
    [InlineData(BaseAddress, "set-automatically-when-deployed", "secret-value")]
    [InlineData(BaseAddress, "case-intake-processor", "")]
    [InlineData(BaseAddress, "case:intake", "secret-value")]
    public async Task SearchComplianceDeclarations_when_not_configured_throws_before_sending(
        string baseAddress,
        string username,
        string password
    )
    {
        var handler = new StubHttpMessageHandler();
        var client = CreateClient(
            handler,
            new WasteObligationsOptions
            {
                BaseAddress = baseAddress,
                Username = username,
                Password = password,
            }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SearchComplianceDeclarations(
                new ComplianceDeclarationSearch { Page = 1, PageSize = 100 },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("WasteObligations", exception.Message);
        Assert.DoesNotContain("secret-value", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void HasReadAccess_when_loopback_http_returns_true()
    {
        var options = new WasteObligationsOptions
        {
            BaseAddress = "http://localhost:8085",
            Username = "Developer",
            Password = s_password,
        };

        Assert.True(options.HasReadAccess);
    }

    private static StubHttpMessageHandler RespondWithSampleWhere(string original, string replacement)
    {
        Assert.True(
            DeclarationsPage.Contains(original, StringComparison.Ordinal),
            "Sample does not contain " + original
        );

        return new StubHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            DeclarationsPage.Replace(original, replacement, StringComparison.Ordinal)
        );
    }

    private static WasteObligationsClient CreateClient(
        StubHttpMessageHandler handler,
        WasteObligationsOptions? options = null
    )
    {
        options ??= new WasteObligationsOptions
        {
            BaseAddress = BaseAddress,
            Username = "case-intake-processor",
            Password = s_password,
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };

        return new WasteObligationsClient(httpClient, Options.Create(options));
    }
}
