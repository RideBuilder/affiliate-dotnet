namespace RideBuilder.Affiliate;

/// <summary>
/// Result of a postback. <c>Accepted</c> is true on a 2xx; <c>Status</c> is the HTTP status (202 = accepted).
/// A 202 means <b>received, not yet validated</b> — RideBuilder validates asynchronously.
/// </summary>
public sealed record PostbackResult(bool Accepted, int Status);

/// <summary>Result of a liveness call (<see cref="RideBuilderClient.HealthCheckAsync"/> / <c>VerifyAsync</c> / <c>HeartbeatAsync</c>).</summary>
public sealed record HealthResult(bool Ok, int Status);

/// <summary>Result of <see cref="RideBuilderClient.RegisterAsync"/> — the stable integration id and status.</summary>
public sealed record RegisterResult(string IntegrationId, string Status);
