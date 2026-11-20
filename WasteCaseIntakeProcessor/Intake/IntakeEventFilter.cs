using Microsoft.Extensions.Options;

namespace WasteCaseIntakeProcessor.Intake;

public sealed class IntakeEventFilter(IOptions<IntakeEventConsumerOptions> options)
{
    private readonly HashSet<string> _handledEventTypes = new(options.Value.HandledEventTypes, StringComparer.Ordinal);

    public bool ShouldProcess(WasteObligationsAnalyticsEvent analyticsEvent) =>
        analyticsEvent.Entity == "compliance_declaration"
        && analyticsEvent.EventType is { } eventType
        && _handledEventTypes.Contains(eventType);
}
