using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using WasteCaseIntakeProcessor.Startup;

namespace WasteCaseIntakeProcessor.Intake;

[ExcludeFromCodeCoverage]
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = options.Value.QueueUrl,
                        MaxNumberOfMessages = 1,
                        VisibilityTimeout = options.Value.VisibilityTimeoutSeconds,
                        MessageAttributeNames = ["All"],
                        WaitTimeSeconds = options.Value.WaitTimeSeconds,
                    },
                    stoppingToken
                );

                foreach (var message in response.Messages ?? [])
                {
                    await ProcessMessage(message, stoppingToken);
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

    private async Task ProcessMessage(Message message, CancellationToken stoppingToken)
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

            await processor.ProcessAsync(analyticsEvent, stoppingToken);
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

    private Task DeleteMessage(Message message, CancellationToken stoppingToken) =>
        sqsClient.DeleteMessageAsync(options.Value.QueueUrl, message.ReceiptHandle, stoppingToken);
}
