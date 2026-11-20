namespace WasteCaseIntakeProcessor.Intake;

public interface IIntakeProcessor
{
    Task ProcessAsync(WasteObligationsAnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}
