using System.Text.Json;

namespace WasteCaseIntakeProcessor.Intake;

public sealed record WasteObligationsAnalyticsEvent(
    string EventId,
    string EntityId,
    string? Entity,
    string? EventType,
    string? SchemaVersion,
    int? Version,
    Guid? OrganisationId,
    string? CorrelationId = null
)
{
    private const string SchemaPrefix = "compliance_declaration_v";
    private static readonly Version MinimumSchema = new(1, 0);

    public bool SupportsDeclarationSchema =>
        SchemaVersion is { } schema
        && schema.StartsWith(SchemaPrefix, StringComparison.Ordinal)
        && System.Version.TryParse(schema[SchemaPrefix.Length..], out var schemaVersion)
        && schemaVersion >= MinimumSchema;

    public static WasteObligationsAnalyticsEvent Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var organisation = Object(Object(root, "after"), "organisation");
        var organisationId = String(organisation, "id");

        return new WasteObligationsAnalyticsEvent(
            RequiredString(root, "eventId"),
            RequiredString(root, "entityId"),
            String(root, "entity"),
            String(root, "eventType"),
            String(root, "schemaVersion"),
            root.TryGetProperty("version", out var version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out var value)
                ? value
                : null,
            Guid.TryParse(organisationId, out var id) ? id : null,
            String(root, "correlationId")
        );
    }

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new IntakeEventProcessingException("invalid-event-envelope");

    private static string? String(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static JsonElement Object(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return default;
        }

        return value.ValueKind == JsonValueKind.Object ? value : default;
    }
}
