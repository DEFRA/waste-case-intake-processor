using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIntakeEventConsumer_when_called_registers_consumer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["IntakeEventConsumer:QueueUrl"] =
                        "http://localhost:4566/000000000000/waste_case_intake_csoc_analytics_events_queue",
                    ["IntakeEventConsumer:ProcessingEnabled"] = "false",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntakeEventConsumer(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(IntakeEventConsumer)
        );

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IntakeEventConsumerOptions>>().Value;
        Assert.Equal(
            "http://localhost:4566/000000000000/waste_case_intake_csoc_analytics_events_queue",
            options.QueueUrl
        );
    }

    [Fact]
    public void AddIntakeEventConsumer_when_queue_url_missing_throws_on_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["IntakeEventConsumer:ProcessingEnabled"] = "false" }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntakeEventConsumer(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<IntakeEventConsumerOptions>>().Value
        );
    }

    [Fact]
    public void AddIntakeEventConsumer_when_aws_service_url_set_registers_sqs_client()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["IntakeEventConsumer:QueueUrl"] =
                        "http://localhost:4566/000000000000/waste_case_intake_csoc_analytics_events_queue",
                    ["IntakeEventConsumer:ProcessingEnabled"] = "false",
                    ["AWS:ServiceURL"] = "http://localhost:4566",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntakeEventConsumer(configuration);

        using var provider = services.BuildServiceProvider();
        var sqs = provider.GetRequiredService<IAmazonSQS>();

        Assert.NotNull(sqs);
    }

    [Fact]
    public void AddIntakeEventConsumer_when_processing_budget_invalid_throws_on_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["IntakeEventConsumer:QueueUrl"] =
                        "http://localhost:4566/000000000000/waste_case_intake_csoc_analytics_events_queue",
                    ["IntakeEventConsumer:ProcessingEnabled"] = "false",
                    ["IntakeEventConsumer:VisibilityTimeoutSeconds"] = "30",
                    ["IntakeEventConsumer:ProcessingTimeoutSeconds"] = "60",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntakeEventConsumer(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<IntakeEventConsumerOptions>>().Value
        );
    }
}
