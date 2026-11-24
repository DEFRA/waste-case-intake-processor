using Microsoft.Extensions.DependencyInjection.Extensions;
using WasteCaseIntakeProcessor.Startup;

namespace WasteCaseIntakeProcessor.Intake;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIntakeEventConsumer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.TryAddSingleton<ApplicationStartup>();

        services
            .AddOptions<IntakeEventConsumerOptions>()
            .Bind(configuration.GetRequiredSection(IntakeEventConsumerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.HandledEventTypes.Length > 0, "At least one handled event type is required")
            .Validate(
                options => options.HasValidProcessingBudget,
                "Visibility timeout must cover the processing budget and the receive timeout must exceed the wait time"
            )
            .ValidateOnStart();

        services.AddSingleton<IntakeEventFilter>();
        services.AddSingleton<IIntakeProcessor, LoggingIntakeProcessor>();
        services.AddAmazonSqsClient(configuration);
        services.AddHostedService<IntakeEventConsumer>();

        return services;
    }
}
