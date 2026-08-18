using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>Options for the hosted heartbeat service.</summary>
public sealed class RideBuilderHeartbeatOptions
{
    /// <summary>How often to send a heartbeat (default 1 hour).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Sends an integration heartbeat on startup and then on a fixed interval, for long-running hosts. The
/// hosted equivalent of <see cref="RideBuilderClient.StartHeartbeat"/>. Failed pings are logged at Debug
/// and swallowed so a transient hiccup never crashes the host.
/// <para>
/// Resolves a <see cref="RideBuilderClient"/> per beat rather than holding one for the host's lifetime:
/// this service is a singleton, and capturing a transient client would pin one
/// <c>IHttpClientFactory</c> handler forever, so DNS changes would never be picked up.
/// </para>
/// </summary>
public sealed class RideBuilderHeartbeatService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeSpan _interval;
    private readonly ILogger<RideBuilderHeartbeatService>? _logger;

    /// <summary>Create the hosted heartbeat service.</summary>
    public RideBuilderHeartbeatService(
        IServiceScopeFactory scopes,
        IOptions<RideBuilderHeartbeatOptions> options,
        ILogger<RideBuilderHeartbeatService>? logger = null)
    {
        _scopes = scopes;
        _interval = options?.Value.Interval ?? TimeSpan.FromHours(1);
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BeatAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await BeatAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // host is shutting down
        }
    }

    private async Task BeatAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<RideBuilderClient>();
            await client.HeartbeatAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "RideBuilder heartbeat failed");
        }
    }
}
