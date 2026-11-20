namespace WasteCaseIntakeProcessor.Intake;

public sealed class IntakeEventProcessingException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
