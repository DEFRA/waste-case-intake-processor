using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace WasteCaseIntakeProcessor.Intake;

internal static class AmazonSqsClientRegistration
{
    public static void AddAmazonSqsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonSQS>(_ => CreateClient(configuration));
    }

    private static AmazonSQSClient CreateClient(IConfiguration configuration)
    {
        var serviceUrl = configuration["AWS:ServiceURL"] ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        var regionName = configuration["AWS:Region"] ?? configuration["AWS_DEFAULT_REGION"] ?? "eu-west-2";

        var config = new AmazonSQSConfig
        {
            AuthenticationRegion = regionName,
            ServiceURL = string.IsNullOrWhiteSpace(serviceUrl) ? null : serviceUrl,
        };

        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            return new AmazonSQSClient(RegionEndpoint.GetBySystemName(regionName));
        }

        return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);
    }
}
