namespace WasteCaseIntakeProcessor.WasteObligations;

public interface IWasteObligationsClient
{
    Task<ComplianceDeclarationsPage> SearchComplianceDeclarations(
        ComplianceDeclarationSearch search,
        CancellationToken cancellationToken
    );
}
