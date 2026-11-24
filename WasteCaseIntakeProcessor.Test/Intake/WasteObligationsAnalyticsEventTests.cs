namespace WasteCaseIntakeProcessor.Test.Intake;

using WasteCaseIntakeProcessor.Intake;

public class WasteObligationsAnalyticsEventTests
{
    private const string SubmissionCreated = """
        {
          "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D6",
          "entity": "compliance_declaration",
          "entityId": "cdec_65f1f6570bb08052a8a27b01",
          "eventType": "submission.created",
          "schemaVersion": "compliance_declaration_v1.3",
          "version": 1,
          "correlationId": "trace-123",
          "after": {
            "organisation": { "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }
          }
        }
        """;

    [Fact]
    public void Parse_when_submission_created_reads_envelope_fields()
    {
        var parsed = WasteObligationsAnalyticsEvent.Parse(SubmissionCreated);

        Assert.Equal("01JZ8RXBMTY2K15SJB3PCFN3D6", parsed.EventId);
        Assert.Equal("cdec_65f1f6570bb08052a8a27b01", parsed.EntityId);
        Assert.Equal("compliance_declaration", parsed.Entity);
        Assert.Equal("submission.created", parsed.EventType);
        Assert.Equal("compliance_declaration_v1.3", parsed.SchemaVersion);
        Assert.Equal(1, parsed.Version);
        Assert.Equal(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), parsed.OrganisationId);
        Assert.True(parsed.SupportsDeclarationSchema);
    }

    [Fact]
    public void Parse_when_event_id_missing_throws()
    {
        const string body = """{ "entityId": "cdec_abc", "eventType": "submission.created" }""";

        Assert.Throws<IntakeEventProcessingException>(() => WasteObligationsAnalyticsEvent.Parse(body));
    }

    [Theory]
    [InlineData("compliance_declaration_v1.0", true)]
    [InlineData("compliance_declaration_v1.3", true)]
    [InlineData("compliance_declaration_v0.9", false)]
    [InlineData("other_entity_v1.0", false)]
    public void SupportsDeclarationSchema_when_schema_version_returns_expected(string schemaVersion, bool expected)
    {
        var parsed = WasteObligationsAnalyticsEvent.Parse(
            $$"""
            {
              "eventId": "e1",
              "entityId": "cdec_x",
              "schemaVersion": "{{schemaVersion}}"
            }
            """
        );

        Assert.Equal(expected, parsed.SupportsDeclarationSchema);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"string\"")]
    public void Parse_when_body_not_object_throws(string body)
    {
        Assert.Throws<IntakeEventProcessingException>(() => WasteObligationsAnalyticsEvent.Parse(body));
    }

    [Fact]
    public void Parse_when_organisation_missing_returns_null_organisation_id()
    {
        var parsed = WasteObligationsAnalyticsEvent.Parse(
            """
            {
              "eventId": "e2",
              "entityId": "cdec_y",
              "eventType": "submission.created"
            }
            """
        );

        Assert.Null(parsed.OrganisationId);
    }
}
