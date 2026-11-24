using System.Text.Json;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeFailureReasonTests
{
    [Theory]
    [InlineData(typeof(IntakeEventProcessingException), true)]
    [InlineData(typeof(JsonException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void IsNonRetryable_when_known_exception_returns_expected(Type exceptionType, bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test")!;

        Assert.Equal(expected, IntakeFailureReason.IsNonRetryable(exception));
    }

    [Fact]
    public void IsNonRetryable_when_timeout_returns_false()
    {
        var exception = new TimeoutException("Intake processing budget exhausted.");

        Assert.False(IntakeFailureReason.IsNonRetryable(exception));
    }
}
