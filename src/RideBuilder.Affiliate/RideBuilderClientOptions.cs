using System;

namespace RideBuilder.Affiliate;

/// <summary>Configuration for <see cref="RideBuilderClient"/>. Mirrors Node <c>RideBuilderClientOptions</c>.</summary>
public sealed class RideBuilderClientOptions
{
    /// <summary>The per-retailer secret API key (64-hex). Required. Store server-side; never in frontend code.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API base URL. Defaults to <c>https://api.ridebuilder.com/v1</c>. A trailing slash is trimmed.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Retries on network errors, timeouts, 5xx, and 429 (default 3). Safe: <c>order_id</c>/<c>return_id</c> are idempotency keys.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Per-attempt timeout (default 10s).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Which environment this integration runs in (default <see cref="RideBuilderEnvironment.Production"/>).</summary>
    public RideBuilderEnvironment Environment { get; set; } = RideBuilderEnvironment.Production;
}
