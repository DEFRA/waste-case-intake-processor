using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using WasteCaseIntakeProcessor.Startup;

namespace WasteCaseIntakeProcessor.Intake;

public sealed class IntakeEventConsumer(
    IAmazonSQS sqsClient,
    IOptions<IntakeEventConsumerOptions> options,
    ILogger<IntakeEventConsumer> logger,
    ApplicationStartup startup,
    IntakeEventFilter filter,
    IIntakeProcessor processor
) : StartupBackgroundService(startup)
{
    protected override async Task ExecuteAfterStartup(CancellationToken stoppingToken)
    {
        if (!options.Value.ProcessingEnabled)
        {
            logger.LogWarning("Intake event consumption is disabled");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);

            return;
        }

        if (!options.Value.HasValidProcessingBudget)
        {
            throw new InvalidOperationException(
                "Intake visibility timeout does not cover its processing budget; check IntakeEventConsumer timeouts."
            );
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await RunBounded(
                    token =>
                        sqsClient.ReceiveMessageAsync(
                            new ReceiveMessageRequest
                            {
                                QueueUrl = options.Value.QueueUrl,
                                MaxNumberOfMessages = 1,
                                VisibilityTimeout = options.Value.VisibilityTimeoutSeconds,
                                MessageAttributeNames = ["All"],
                                WaitTimeSeconds = options.Value.WaitTimeSeconds,
                            },
                            token
                        ),
                    options.Value.ReceiveTimeoutSeconds,
                    stoppingToken
                );

                var receivedAt = Stopwatch.GetTimestamp();
                foreach (var message in response.Messages ?? [])
                {
                    await ProcessMessage(message, receivedAt, stoppingToken);
                }
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Intake queue receive failed; retrying after {PollIntervalSeconds}s",
                    options.Value.PollIntervalSeconds
                );
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task ProcessMessage(Message message, long receivedAt, CancellationToken stoppingToken)
    {
        var eventId = (string?)null;
        var entityId = (string?)null;
        try
        {
            var body = AnalyticsMessageBodyReader.Read(message);
            var analyticsEvent = WasteObligationsAnalyticsEvent.Parse(body);
            eventId = analyticsEvent.EventId;
            entityId = analyticsEvent.EntityId;

            if (!filter.ShouldProcess(analyticsEvent))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Ignored analytics event {EventId} for entity {EntityId} ({EventType})",
                        eventId,
                        entityId,
                        analyticsEvent.EventType
                    );
                }

                await DeleteMessage(message, stoppingToken);
                return;
            }

            EnsureRemaining(receivedAt, options.Value.ProcessingTimeoutSeconds + options.Value.DeleteTimeoutSeconds);
            await RunBounded(
                token => processor.ProcessAsync(analyticsEvent, token),
                options.Value.ProcessingTimeoutSeconds,
                stoppingToken
            );

            EnsureRemaining(receivedAt, options.Value.DeleteTimeoutSeconds);
            await DeleteMessage(message, stoppingToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Consumed intake analytics event {EventId} for entity {EntityId}",
                    eventId,
                    entityId
                );
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            if (IntakeFailureReason.IsNonRetryable(exception))
            {
                logger.LogWarning(
                    exception,
                    "Intake event processing failed for event {EventId}, entity {EntityId}; acknowledging non-retryable message",
                    eventId,
                    entityId
                );
                await DeleteMessage(message, stoppingToken);
                return;
            }

            logger.LogWarning(
                exception,
                "Intake event processing failed for event {EventId}, entity {EntityId}; message left on queue for retry",
                eventId,
                entityId
            );
        }
    }

    private Task<DeleteMessageResponse> DeleteMessage(Message message, CancellationToken stoppingToken) =>
        RunBounded(
            token => sqsClient.DeleteMessageAsync(options.Value.QueueUrl, message.ReceiptHandle, token),
            options.Value.DeleteTimeoutSeconds,
            stoppingToken
        );

    /// <summary>
    /// Fail fast while a message is still invisible if the remaining visibility budget can no longer
    /// cover the next step plus safety headroom, so the message is left for a clean redelivery instead
    /// of being processed past its visibility timeout and risking a duplicate.
    /// </summary>
    private void EnsureRemaining(long receivedAt, int budgetSeconds)
    {
        var elapsed = Stopwatch.GetElapsedTime(receivedAt);
        var required = TimeSpan.FromSeconds(budgetSeconds + options.Value.SafetyHeadroomSeconds);
        if (elapsed + required >= TimeSpan.FromSeconds(options.Value.VisibilityTimeoutSeconds))
        {
            throw new TimeoutException("Intake processing budget exhausted.");
        }
    }

    private static async Task<T> RunBounded<T>(
        Func<CancellationToken, Task<T>> operation,
        int seconds,
        CancellationToken stoppingToken
    )
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        source.CancelAfter(TimeSpan.FromSeconds(seconds));
        var startedAt = Stopwatch.GetTimestamp();
        var result = await operation(source.Token).WaitAsync(source.Token);
        stoppingToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(startedAt) >= TimeSpan.FromSeconds(seconds))
        {
            throw new TimeoutException("Intake dependency exceeded its timeout.");
        }

        return result;
    }

    private static Task<object?> RunBounded(
        Func<CancellationToken, Task> operation,
        int seconds,
        CancellationToken stoppingToken
    ) =>
        RunBounded<object?>(
            async token =>
            {
                await operation(token);
                return null;
            },
            seconds,
            stoppingToken
        );
}
