using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeEventConsumerOptionsTests
{
    [Fact]
    public void HasValidProcessingBudget_when_defaults_returns_true()
    {
        var options = new IntakeEventConsumerOptions { QueueUrl = "http://localhost/queue" };

        Assert.True(options.HasValidProcessingBudget);
    }

    [Fact]
    public void HasValidProcessingBudget_when_visibility_too_small_returns_false()
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
    public void HasValidProcessingBudget_when_receive_timeout_not_above_wait_returns_false()
    {
        var options = new IntakeEventConsumerOptions
        {
            QueueUrl = "http://localhost/queue",
            WaitTimeSeconds = 20,
            ReceiveTimeoutSeconds = 20,
        };

        Assert.False(options.HasValidProcessingBudget);
    }

    [Fact]
    public void HasValidProcessingBudget_when_visibility_equals_budget_sum_returns_false()
    {
        // Defaults: Processing(60) + Delete(5) + Headroom(10) = 75; must be strictly greater than EnsureRemaining allows.
        var options = new IntakeEventConsumerOptions
        {
            QueueUrl = "http://localhost/queue",
            VisibilityTimeoutSeconds = 75,
        };

        Assert.False(options.HasValidProcessingBudget);
    }

    [Fact]
    public void HasValidProcessingBudget_when_visibility_one_above_budget_returns_true()
    {
        var options = new IntakeEventConsumerOptions
        {
            QueueUrl = "http://localhost/queue",
            VisibilityTimeoutSeconds = 76,
        };

        Assert.True(options.HasValidProcessingBudget);
    }
}
