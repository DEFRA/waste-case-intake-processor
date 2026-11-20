using System.Text.Json;

namespace WasteCaseIntakeProcessor.Intake;

public static class IntakeFailureReason
{
    public static bool IsNonRetryable(Exception exception) =>
        exception switch
        {
            IntakeEventProcessingException => true,
            JsonException => true,
            InvalidOperationException invalid
                when invalid.Message.Contains("content encoding", StringComparison.Ordinal) => true,
            _ => false,
        };
}
