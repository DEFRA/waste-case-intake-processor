using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WasteCaseIntakeProcessor.Intake;
using WasteCaseIntakeProcessor.Startup;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeEventConsumerTests
{
    private const string QueueUrl = "http://test/q";

    // A body that parses successfully but does not match the event filter
    private const string FilteredBody =
        """{"eventId":"e1","entityId":"cdec_x","entity":"other_entity","eventType":"submission.created"}""";

    // A body that parses and matches the event filter
    private const string MatchedBody =
        """{"eventId":"e2","entityId":"cdec_y","entity":"compliance_declaration","eventType":"submission.created","schemaVersion":"compliance_declaration_v1.0"}""";

    private static ApplicationStartup StartedStartup()
    {
        var startup = new ApplicationStartup();
        startup.MarkStarted();
        return startup;
    }

    /// <summary>
    /// Sets up IAmazonSQS to return one message on the first receive, then cancel via the returned
    /// CancellationTokenSource on the second, stopping the consumer cleanly.
    /// </summary>
    private static (IAmazonSQS sqsClient, CancellationTokenSource cts) SqsWithOneMessage(Message message)
    {
        var cts = new CancellationTokenSource();
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(new ReceiveMessageResponse { Messages = [message] }),
                _ =>
                {
                    cts.Cancel();
                    return Task.FromCanceled<ReceiveMessageResponse>(cts.Token);
                }
            );
        sqsClient
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteMessageResponse());
        return (sqsClient, cts);
    }

    private static IntakeEventConsumer BuildConsumer(
        IAmazonSQS sqsClient,
        IntakeEventConsumerOptions? options = null,
        IIntakeProcessor? processor = null,
        ILogger<IntakeEventConsumer>? logger = null
    )
    {
        var opts = options ?? new IntakeEventConsumerOptions { QueueUrl = QueueUrl, ProcessingEnabled = true };
        var optionsWrapper = Options.Create(opts);
        return new IntakeEventConsumer(
            sqsClient,
            optionsWrapper,
            logger ?? Substitute.For<ILogger<IntakeEventConsumer>>(),
            StartedStartup(),
            new IntakeEventFilter(optionsWrapper),
            processor ?? Substitute.For<IIntakeProcessor>()
        );
    }

    [Fact]
    public async Task ProcessingEnabled_false_disables_consuming()
    {
        var sqsClient = Substitute.For<IAmazonSQS>();
        var opts = new IntakeEventConsumerOptions { QueueUrl = QueueUrl, ProcessingEnabled = false };
        var consumer = BuildConsumer(sqsClient, opts);

        await consumer.StartAsync(CancellationToken.None);
        await consumer.StopAsync(CancellationToken.None);

        await sqsClient
            .DidNotReceive()
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidProcessingBudget_throws()
    {
        var sqsClient = Substitute.For<IAmazonSQS>();
        // VisibilityTimeoutSeconds (1) < ProcessingTimeoutSeconds + DeleteTimeoutSeconds + SafetyHeadroomSeconds (75)
        var opts = new IntakeEventConsumerOptions
        {
            QueueUrl = QueueUrl,
            ProcessingEnabled = true,
            VisibilityTimeoutSeconds = 1,
        };
        var consumer = BuildConsumer(sqsClient, opts);

        await consumer.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ExecuteTask!);
    }

    [Fact]
    public async Task Ignored_message_is_deleted_without_processing()
    {
        var message = new Message { Body = FilteredBody, ReceiptHandle = "rh-1" };
        var (sqsClient, cts) = SqsWithOneMessage(message);
        var processor = Substitute.For<IIntakeProcessor>();
        var logger = Substitute.For<ILogger<IntakeEventConsumer>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        var consumer = BuildConsumer(sqsClient, processor: processor, logger: logger);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient.Received(1).DeleteMessageAsync(QueueUrl, "rh-1", Arg.Any<CancellationToken>());
        await processor
            .DidNotReceive()
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Processed_message_is_deleted()
    {
        var message = new Message { Body = MatchedBody, ReceiptHandle = "rh-2" };
        var (sqsClient, cts) = SqsWithOneMessage(message);
        var processor = Substitute.For<IIntakeProcessor>();
        processor
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var logger = Substitute.For<ILogger<IntakeEventConsumer>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        var consumer = BuildConsumer(sqsClient, processor: processor, logger: logger);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient.Received(1).DeleteMessageAsync(QueueUrl, "rh-2", Arg.Any<CancellationToken>());
        await processor
            .Received(1)
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonRetryable_error_deletes_message()
    {
        var message = new Message { Body = MatchedBody, ReceiptHandle = "rh-3" };
        var (sqsClient, cts) = SqsWithOneMessage(message);
        var processor = Substitute.For<IIntakeProcessor>();
        processor
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IntakeEventProcessingException("test-error")));

        var consumer = BuildConsumer(sqsClient, processor: processor);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient.Received(1).DeleteMessageAsync(QueueUrl, "rh-3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retryable_error_leaves_message_on_queue()
    {
        var message = new Message { Body = MatchedBody, ReceiptHandle = "rh-4" };
        var (sqsClient, cts) = SqsWithOneMessage(message);
        var processor = Substitute.For<IIntakeProcessor>();
        processor
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("transient")));

        var consumer = BuildConsumer(sqsClient, processor: processor);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient
            .DidNotReceive()
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Processor_timeout_leaves_message_on_queue()
    {
        // TimeoutException (from EnsureRemaining or a timed-out dependency) is retryable:
        // the message is left on the queue, not deleted.
        var message = new Message { Body = MatchedBody, ReceiptHandle = "rh-5" };
        var (sqsClient, cts) = SqsWithOneMessage(message);
        var processor = Substitute.For<IIntakeProcessor>();
        processor
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("Intake dependency exceeded its timeout.")));

        var consumer = BuildConsumer(sqsClient, processor: processor);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient
            .DidNotReceive()
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_failure_logs_warning_and_continues()
    {
        var message = new Message { Body = MatchedBody, ReceiptHandle = "rh-del" };
        var (sqsClient, cts) = SqsWithOneMessage(message);

        // Override the delete setup from SqsWithOneMessage to throw instead
        sqsClient
            .DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeleteMessageResponse>(new Exception("SQS unavailable")));

        var processor = Substitute.For<IIntakeProcessor>();
        processor
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var consumer = BuildConsumer(sqsClient, processor: processor);
        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!; // delete failure must not propagate

        await processor
            .Received(1)
            .ProcessAsync(Arg.Any<WasteObligationsAnalyticsEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_error_logs_warning_and_retries()
    {
        using var cts = new CancellationTokenSource();
        var sqsClient = Substitute.For<IAmazonSQS>();
        sqsClient
            .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<ReceiveMessageResponse>(new Exception("network error")),
                _ =>
                {
                    cts.Cancel();
                    return Task.FromCanceled<ReceiveMessageResponse>(cts.Token);
                }
            );

        // PollIntervalSeconds = 0 → Task.Delay(TimeSpan.Zero) completes immediately so the test stays fast
        var opts = new IntakeEventConsumerOptions
        {
            QueueUrl = QueueUrl,
            ProcessingEnabled = true,
            PollIntervalSeconds = 0,
        };
        var consumer = BuildConsumer(sqsClient, opts);

        await consumer.StartAsync(cts.Token);
        await consumer.ExecuteTask!;

        await sqsClient.Received(2).ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>());
    }
}
