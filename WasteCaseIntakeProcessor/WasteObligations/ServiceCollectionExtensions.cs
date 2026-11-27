using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;

namespace WasteCaseIntakeProcessor.WasteObligations;

public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan s_bodyReadMargin = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers the Waste Obligations client. Settings are not validated at startup, so the service still starts
    /// where they are not set yet; the client refuses to send until they are.
    /// </summary>
    public static IServiceCollection AddWasteObligationsClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<WasteObligationsOptions>()
            .Bind(configuration.GetSection(WasteObligationsOptions.SectionName));

        var httpClient = services
            .AddHttpClient<IWasteObligationsClient, WasteObligationsClient>(
                (provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<WasteObligationsOptions>>().Value;
                    if (options.HasReadAccess)
                        client.BaseAddress = options.BaseUri;

                    // A backstop only: the resilience timeouts already cover the body (see ResponseBufferingHandler).
                    // The margin lets them report first.
                    client.Timeout = options.TotalRequestTimeout.Timeout + s_bodyReadMargin;
                }
            )
            // Waste Obligations is an internal service, so calls don't go through the outbound proxy.
            // No header propagation: the client is called from background work, outside any request.
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    UseProxy = false,
                }
            )
            // Default HTTP failure logs include raw exception text, which can carry declaration data.
            .RemoveAllLoggers();

        httpClient.AddResilienceHandler(
            WasteObligationsOptions.SectionName,
            (pipeline, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<WasteObligationsOptions>>().Value;
                pipeline.ConfigureTelemetry(NullLoggerFactory.Instance);
                pipeline
                    .AddTimeout(options.TotalRequestTimeout)
                    .AddRetry(options.Retry)
                    .AddTimeout(options.AttemptTimeout);
            }
        );

        // Added after the resilience handler, so it runs inside each attempt.
        httpClient.AddHttpMessageHandler(() => new ResponseBufferingHandler());

        return services;
    }
}
