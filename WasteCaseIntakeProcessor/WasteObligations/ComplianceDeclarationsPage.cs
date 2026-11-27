namespace WasteCaseIntakeProcessor.WasteObligations;

// Only what is needed to store a row is required: the page itself, and each declaration's id and version.
// Every other field may be missing or null, so one incomplete declaration is recorded as invalid by the import
// instead of failing its whole page. Values of the wrong type still fail the page.

public sealed record ComplianceDeclarationsPage(
    IReadOnlyList<ComplianceDeclaration> ComplianceDeclarations,
    int Total,
    int Page,
    int PageSize
);

public sealed record ComplianceDeclaration(
    string Id,
    int Version,
    DateTimeOffset? Created = null,
    DateTimeOffset? Updated = null,
    string? Status = null,
    int? ObligationYear = null,
    string? SubmitterName = null,
    DeclarationOrganisation? Organisation = null,
    IReadOnlyList<DeclarationAuditEntry>? Audit = null
);

public sealed record DeclarationOrganisation(
    Guid? Id = null,
    string? RegistrationType = null,
    string? Regulator = null,
    string? Name = null,
    string? SchemeOperatorName = null,
    string? ReferenceNumber = null,
    string? BusinessCountry = null
);

/// <summary>
/// An entry in a declaration's audit. Action is the status it moved to. Reason is set when Waste Obligations
/// recorded one (always on cancellation).
/// </summary>
public sealed record DeclarationAuditEntry(
    string? Action = null,
    DateTimeOffset? Timestamp = null,
    DeclarationUser? User = null,
    string? Reason = null
);

public sealed record DeclarationUser(string? Id = null, string? Email = null, string? Name = null);
