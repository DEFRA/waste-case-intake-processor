using System.ComponentModel.DataAnnotations;

namespace WasteCaseIntakeProcessor.Intake;

public sealed class IntakeEventConsumerOptions
{
    public const string SectionName = "IntakeEventConsumer";

    [Required]
    public required string QueueUrl { get; init; }

    public bool ProcessingEnabled { get; init; }

    [Range(1, 20)]
    public int WaitTimeSeconds { get; init; } = 20;

    [Range(1, 300)]
    public int ReceiveTimeoutSeconds { get; init; } = 30;

    [Range(1, 300)]
    public int ProcessingTimeoutSeconds { get; init; } = 60;

    [Range(1, 300)]
    public int DeleteTimeoutSeconds { get; init; } = 5;

    [Range(1, 300)]
    public int SafetyHeadroomSeconds { get; init; } = 10;

    [Range(1, 43200)]
    public int VisibilityTimeoutSeconds { get; init; } = 120;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; init; } = 15;

    public string[] HandledEventTypes { get; init; } = ["submission.created"];

    /// <summary>
    /// The receive long poll must complete before its own timeout, and the work done while a
    /// message is invisible (processing + delete + safety headroom) must fit inside the visibility
    /// timeout, so a slow dependency cannot let SQS re-deliver a message that is still in flight.
    /// </summary>
    public bool HasValidProcessingBudget =>
        ReceiveTimeoutSeconds > WaitTimeSeconds
        && VisibilityTimeoutSeconds > ProcessingTimeoutSeconds + DeleteTimeoutSeconds + SafetyHeadroomSeconds;
}
