using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>DI registration for the RideBuilder Affiliate SDK.</summary>
public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "RideBuilder.Affiliate";

    /// <summary>
    /// Register a <see cref="RideBuilderClient"/> backed by <c>IHttpClientFactory</c> (so handler lifetime
    /// and pooling are managed for you). Returns a builder for optional add-ons like
    /// <see cref="RideBuilderBuilderExtensions.AddHeartbeat"/>.
    /// </summary>
    public static IRideBuilderBuilder AddRideBuilder(this IServiceCollection services, Action<RideBuilderClientOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        services.Configure(configure);
        services.AddHttpClient(HttpClientName);

        // Transient client composed with a factory-managed HttpClient — the recommended pairing (short-lived
        // client, factory-owned handler). Injecting it into a singleton (e.g. the hosted heartbeat) captures
        // one instance for that singleton's lifetime, which is fine for this SDK.
        services.AddTransient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RideBuilderClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new RideBuilderClient(options, http);
        });

        return new RideBuilderBuilder(services);
    }

    /// <summary>Configure the click_id capture middleware (item key, on-capture callback).</summary>
    public static IServiceCollection ConfigureRideBuilderCapture(this IServiceCollection services, Action<RideBuilderCaptureOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}

/// <summary>Builder returned by <see cref="ServiceCollectionExtensions.AddRideBuilder"/> for chaining add-ons.</summary>
public interface IRideBuilderBuilder
{
    /// <summary>The underlying service collection.</summary>
    IServiceCollection Services { get; }
}

internal sealed class RideBuilderBuilder : IRideBuilderBuilder
{
    public RideBuilderBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }
}

/// <summary>Add-ons chained off <see cref="ServiceCollectionExtensions.AddRideBuilder"/>.</summary>
public static class RideBuilderBuilderExtensions
{
    /// <summary>
    /// Run a background service that heartbeats on startup and every <paramref name="interval"/>
    /// (default hourly) for the life of the host.
    /// </summary>
    public static IRideBuilderBuilder AddHeartbeat(this IRideBuilderBuilder builder, TimeSpan? interval = null)
    {
        builder.Services.Configure<RideBuilderHeartbeatOptions>(o =>
        {
            if (interval.HasValue)
                o.Interval = interval.Value;
        });
        builder.Services.AddHostedService<RideBuilderHeartbeatService>();
        return builder;
    }
}
