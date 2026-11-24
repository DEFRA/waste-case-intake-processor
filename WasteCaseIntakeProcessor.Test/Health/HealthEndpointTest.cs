using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteCaseIntakeProcessor.Test.Health;

public class HealthEndpointTest
{
    [Fact]
    public async Task Get_when_called_returns_ok_and_healthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AWS:ServiceURL", "http://localhost:4566");
            builder.UseSetting("AWS:Region", "eu-west-2");
            builder.UseSetting("IntakeEventConsumer:ProcessingEnabled", "false");
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
