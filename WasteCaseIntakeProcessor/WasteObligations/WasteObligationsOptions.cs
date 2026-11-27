using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace WasteCaseIntakeProcessor.WasteObligations;

/// <summary>
/// Waste Obligations connection settings. Retry and timeouts can be overridden under the same section
/// (for example WasteObligations:Retry:MaxRetryAttempts).
/// </summary>
public sealed class WasteObligationsOptions : HttpStandardResilienceOptions
{
    public const string SectionName = "WasteObligations";

    private const string Placeholder = "set-automatically";

    public WasteObligationsOptions()
    {
        // Each attempt includes reading the response body. A page of 100 full declarations is larger than the
        // single reads other services make.
        TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        Retry.MaxRetryAttempts = 3;
        Retry.Delay = TimeSpan.FromSeconds(1);
        Retry.BackoffType = DelayBackoffType.Exponential;
        Retry.UseJitter = true;
        Retry.DisableForUnsafeHttpMethods();
    }

    public string BaseAddress { get; init; } = "set-automatically-when-deployed";

    public string Username { get; init; } = "set-automatically-when-deployed";

    public string Password { get; init; } = "set-automatically-when-deployed";

    /// <summary>
    /// True when the base address is HTTPS (or HTTP to the local machine) and Basic credentials are set.
    /// </summary>
    public bool HasReadAccess =>
        SecureAddress(BaseAddress) && Configured(Username) && Configured(Password) && !Username.Contains(':');

    /// <summary>The base address with a trailing slash, so relative paths keep its last segment.</summary>
    public Uri BaseUri
    {
        get
        {
            var address = new UriBuilder(BaseAddress);
            if (!address.Path.EndsWith('/'))
                address.Path += "/";

            return address.Uri;
        }
    }

    private static bool SecureAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo);

    private static bool Configured(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith(Placeholder, StringComparison.Ordinal);
}
