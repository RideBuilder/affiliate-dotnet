namespace RideBuilder.Affiliate;

/// <summary>
/// Which environment this integration runs in. Sent on <c>register</c>/<c>heartbeat</c> so RideBuilder
/// keeps test traffic separate from production. Maps to the wire values <c>"production"</c>/<c>"sandbox"</c>.
/// </summary>
public enum RideBuilderEnvironment
{
    /// <summary>Live traffic (default).</summary>
    Production = 0,

    /// <summary>Test traffic, kept separate from production.</summary>
    Sandbox = 1,
}
