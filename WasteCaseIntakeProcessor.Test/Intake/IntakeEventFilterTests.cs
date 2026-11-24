using Microsoft.Extensions.Options;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class IntakeEventFilterTests
{
    [Theory]
    [InlineData("submission.created", true)]
    [InlineData("submission.amended", false)]
    public void ShouldProcess_when_event_type_returns_expected(string eventType, bool expected)
    {
        var filter = CreateFilter(["submission.created"]);
        var analyticsEvent = new WasteObligationsAnalyticsEvent(
            "01JZ8RXBMTY2K15SJB3PCFN3D6",
            "cdec_65f1f6570bb08052a8a27b01",
            "compliance_declaration",
            eventType,
            "compliance_declaration_v1.3",
            1,
            Guid.NewGuid()
        );

        Assert.Equal(expected, filter.ShouldProcess(analyticsEvent));
    }

    [Fact]
    public void ShouldProcess_when_non_declaration_entity_returns_false()
    {
        var filter = CreateFilter(["submission.created"]);
        var analyticsEvent = new WasteObligationsAnalyticsEvent(
            "01JZ8RXBMTY2K15SJB3PCFN3D6",
            "other_abc",
            "other_entity",
            "submission.created",
            "compliance_declaration_v1.3",
            1,
            Guid.NewGuid()
        );

        Assert.False(filter.ShouldProcess(analyticsEvent));
    }

    private static IntakeEventFilter CreateFilter(string[] handledEventTypes) =>
        new(
            Options.Create(
                new IntakeEventConsumerOptions
                {
                    QueueUrl = "http://localhost:4566/000000000000/waste_case_intake_csoc_analytics_events_queue",
                    HandledEventTypes = handledEventTypes,
                }
            )
        );
}
