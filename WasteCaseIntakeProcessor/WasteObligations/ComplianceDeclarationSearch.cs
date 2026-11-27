namespace WasteCaseIntakeProcessor.WasteObligations;

/// <summary>
/// One page of the Waste Obligations compliance declaration search. Filters left null are not sent.
/// No sort is sent, so Waste Obligations orders by declaration id.
/// </summary>
public sealed record ComplianceDeclarationSearch
{
    public const int MaximumPageSize = 100;

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public ComplianceDeclarationStatus? Status { get; init; }

    public int? ObligationYear { get; init; }

    public RegistrationType? RegistrationType { get; init; }

    public BusinessCountry? Country { get; init; }
}

public enum ComplianceDeclarationStatus
{
    Submitted,
    Accepted,
    Cancelled,
}

public enum RegistrationType
{
    DirectProducer,
    ComplianceScheme,
}

public enum BusinessCountry
{
    England,
    NorthernIreland,
    Scotland,
    Wales,
}
