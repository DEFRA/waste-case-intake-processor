using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeEventConsumerOptionsTests
{
    [Fact]
    public void HasValidProcessingBudget_is_true_for_defaults()
    {
        var options = new IntakeEventConsumerOptions { QueueUrl = "http://localhost/queue" };

        Assert.True(options.HasValidProcessingBudget);
    }

    [Fact]
    public void HasValidProcessingBudget_is_false_when_visibility_too_small()
    {
        var options = new IntakeEventConsumerOptions
        {
            QueueUrl = "http://localhost/queue",
            VisibilityTimeoutSeconds = 30,
            ProcessingTimeoutSeconds = 60,
            DeleteTimeoutSeconds = 5,
            SafetyHeadroomSeconds = 10,
        };

        Assert.False(options.HasValidProcessingBudget);
    }

    [Fact]
    public void HasValidProcessingBudget_is_false_when_receive_timeout_not_above_wait()
    {
        var options = new IntakeEventConsumerOptions
        {
            QueueUrl = "http://localhost/queue",
            WaitTimeSeconds = 20,
            ReceiveTimeoutSeconds = 20,
        };

        Assert.False(options.HasValidProcessingBudget);
    }
}
