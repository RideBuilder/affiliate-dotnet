using System;
using System.Threading;
using System.Threading.Tasks;
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
/// </summary>
public sealed class RideBuilderHeartbeatService : BackgroundService
{
    private readonly RideBuilderClient _client;
    private readonly TimeSpan _interval;
    private readonly ILogger<RideBuilderHeartbeatService>? _logger;

    /// <summary>Create the hosted heartbeat service.</summary>
    public RideBuilderHeartbeatService(
        RideBuilderClient client,
        IOptions<RideBuilderHeartbeatOptions> options,
        ILogger<RideBuilderHeartbeatService>? logger = null)
    {
        _client = client;
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
            await _client.HeartbeatAsync(ct).ConfigureAwait(false);
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
