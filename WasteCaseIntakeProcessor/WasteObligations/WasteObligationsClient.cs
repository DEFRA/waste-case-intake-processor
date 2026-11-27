using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WasteCaseIntakeProcessor.WasteObligations;

public sealed class WasteObligationsClient(HttpClient client, IOptions<WasteObligationsOptions> options)
    : IWasteObligationsClient
{
    // Enforces the non-nullable members of the response model: the page, and each declaration's id and version.
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public async Task<ComplianceDeclarationsPage> SearchComplianceDeclarations(
        ComplianceDeclarationSearch search,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(search.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(search.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(search.PageSize, ComplianceDeclarationSearch.MaximumPageSize);

        using var request = CreateRequest(SearchPath(search));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ComplianceDeclarationsPage>(s_jsonOptions, cancellationToken)
            ?? throw new JsonException("Compliance declarations response is missing.");
    }

    private static string SearchPath(ComplianceDeclarationSearch search)
    {
        var query = new StringBuilder("compliance-declarations?page=")
            .Append(search.Page.ToString(CultureInfo.InvariantCulture))
            .Append("&pageSize=")
            .Append(search.PageSize.ToString(CultureInfo.InvariantCulture));

        if (search.Status is { } status)
            query.Append("&status=").Append(status.ToString());

        if (search.ObligationYear is { } obligationYear)
            query.Append("&obligationYear=").Append(obligationYear.ToString(CultureInfo.InvariantCulture));

        if (search.RegistrationType is { } registrationType)
            query.Append("&registrationType=").Append(registrationType.ToString());

        if (search.Country is { } country)
            query.Append("&country=").Append(CountryCode(country));

        return query.ToString();
    }

    private static string CountryCode(BusinessCountry country) =>
        country switch
        {
            BusinessCountry.England => "GB-ENG",
            BusinessCountry.NorthernIreland => "GB-NIR",
            BusinessCountry.Scotland => "GB-SCT",
            BusinessCountry.Wales => "GB-WLS",
            _ => throw new ArgumentOutOfRangeException(nameof(country), country, "Unknown business country"),
        };

    private HttpRequestMessage CreateRequest(string path)
    {
        var settings = options.Value;
        if (!settings.HasReadAccess)
            throw new InvalidOperationException(
                $"{WasteObligationsOptions.SectionName} needs an HTTPS (or local HTTP) BaseAddress and Basic credentials before declarations can be read."
            );

        var request = new HttpRequestMessage(HttpMethod.Get, path)
        {
            Version = client.BaseAddress?.Scheme == Uri.UriSchemeHttps ? HttpVersion.Version20 : HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"))
        );

        return request;
    }
}
