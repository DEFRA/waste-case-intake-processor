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

    [Range(1, 43200)]
    public int VisibilityTimeoutSeconds { get; init; } = 120;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; init; } = 15;

    public string[] HandledEventTypes { get; init; } = ["submission.created"];
}
