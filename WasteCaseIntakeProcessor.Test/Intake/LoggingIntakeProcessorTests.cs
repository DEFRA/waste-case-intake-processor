using Microsoft.Extensions.Logging;
using NSubstitute;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class LoggingIntakeProcessorTests
{
    [Fact]
    public async Task ProcessAsync_when_logging_enabled_completes()
    {
        var logger = Substitute.For<ILogger<LoggingIntakeProcessor>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);
        var processor = new LoggingIntakeProcessor(logger);
        var analyticsEvent = new WasteObligationsAnalyticsEvent(
            "ev-1",
            "cdec_abc",
            "compliance_declaration",
            "submission.created",
            null,
            null,
            Guid.NewGuid()
        );

        await processor.ProcessAsync(analyticsEvent, CancellationToken.None);

        logger.Received(1).IsEnabled(LogLevel.Information);
    }

    [Fact]
    public async Task ProcessAsync_when_logging_disabled_still_completes()
    {
        var logger = Substitute.For<ILogger<LoggingIntakeProcessor>>();
        logger.IsEnabled(LogLevel.Information).Returns(false);
        var processor = new LoggingIntakeProcessor(logger);
        var analyticsEvent = new WasteObligationsAnalyticsEvent("ev-2", "cdec_def", null, null, null, null, null);

        await processor.ProcessAsync(analyticsEvent, CancellationToken.None);

        logger.Received(1).IsEnabled(LogLevel.Information);
    }

    [Fact]
    public async Task ProcessAsync_when_cancelled_throws()
    {
        var processor = new LoggingIntakeProcessor(Substitute.For<ILogger<LoggingIntakeProcessor>>());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(
                new WasteObligationsAnalyticsEvent("ev-3", "cdec_ghi", null, null, null, null, null),
                source.Token
            )
        );
    }
}
