using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteCaseIntakeProcessor.Test.Health;

public class HealthEndpointTest
{
    [Fact]
    public async Task Get_returns_ok_and_healthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
