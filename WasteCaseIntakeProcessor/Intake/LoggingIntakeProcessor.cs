namespace WasteCaseIntakeProcessor.Intake;

public sealed class LoggingIntakeProcessor(ILogger<LoggingIntakeProcessor> logger) : IIntakeProcessor
{
    public Task ProcessAsync(WasteObligationsAnalyticsEvent analyticsEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Intake event {EventId} for entity {EntityId} ({EventType}) is queued for case creation (processor stub)",
                analyticsEvent.EventId,
                analyticsEvent.EntityId,
                analyticsEvent.EventType
            );
        }

        return Task.CompletedTask;
    }
}
