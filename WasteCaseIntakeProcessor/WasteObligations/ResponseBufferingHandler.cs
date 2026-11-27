namespace WasteCaseIntakeProcessor.WasteObligations;

/// <summary>
/// Reads the whole response body inside the resilience pipeline, so each attempt's timeout and the retry cover the
/// body as well as the headers. Without it a body that fails or stalls part way is not retried.
/// </summary>
public sealed class ResponseBufferingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
