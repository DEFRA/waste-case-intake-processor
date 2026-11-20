using System.Text.Json;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeFailureReasonTests
{
    [Theory]
    [InlineData(typeof(IntakeEventProcessingException), true)]
    [InlineData(typeof(JsonException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void IsNonRetryable_classifies_known_exceptions(Type exceptionType, bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test")!;

        Assert.Equal(expected, IntakeFailureReason.IsNonRetryable(exception));
    }

    [Fact]
    public void IsNonRetryable_treats_unsupported_content_encoding_as_non_retryable()
    {
        var exception = new InvalidOperationException(
            "Analytics event message content encoding 'br' is not supported."
        );

        Assert.True(IntakeFailureReason.IsNonRetryable(exception));
    }
}
