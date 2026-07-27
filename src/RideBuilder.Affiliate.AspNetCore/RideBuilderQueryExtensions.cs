using Microsoft.AspNetCore.Http;
using RideBuilder.Affiliate.Validation;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>ASP.NET Core overloads of the framework-free capture helpers.</summary>
public static class RideBuilderQueryExtensions
{
    /// <summary>
    /// Read a validated click_id off an <see cref="IQueryCollection"/>. Returns <c>null</c> unless
    /// <c>ref == "ridebuilder"</c> and <c>click_id</c> is a valid UUID v4 — the same rules as
    /// <see cref="Capture.ClickCapture.FromQuery"/>.
    /// </summary>
    public static string? GetRideBuilderClickId(this IQueryCollection query)
    {
        if (query is null)
            return null;

        var reff = query.TryGetValue("ref", out var r) ? r.ToString() : null;
        if (reff != ClickId.Ref)
            return null;

        var clickId = query.TryGetValue("click_id", out var c) ? c.ToString() : null;
        return ClickId.IsValid(clickId) ? clickId : null;
    }
}
