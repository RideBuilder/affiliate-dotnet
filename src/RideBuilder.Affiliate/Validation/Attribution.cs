namespace RideBuilder.Affiliate.Validation;

/// <summary>
/// The attribution payload the browser snippet stores in the <c>ridebuilder_attribution</c> cookie.
/// Mirrors the Node <c>Attribution</c> interface (src/validation.ts).
/// </summary>
/// <param name="ClickId">The captured click_id (UUID v4).</param>
/// <param name="Ref">Always <see cref="RideBuilder.Affiliate.Validation.ClickId.Ref"/>.</param>
/// <param name="ClickedAt">ISO-8601 timestamp the snippet wrote at capture time; carried through, not validated.</param>
public sealed record Attribution(string ClickId, string Ref, string? ClickedAt);
