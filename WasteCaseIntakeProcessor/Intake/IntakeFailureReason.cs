using System.Text.Json;

namespace WasteCaseIntakeProcessor.Intake;

public static class IntakeFailureReason
{
    public static bool IsNonRetryable(Exception exception) =>
        exception switch
        {
            IntakeEventProcessingException => true,
            JsonException => true,
            _ => false,
        };
}
