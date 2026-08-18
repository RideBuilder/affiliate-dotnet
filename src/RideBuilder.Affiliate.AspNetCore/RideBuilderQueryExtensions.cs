using Microsoft.AspNetCore.Http;
using RideBuilder.Affiliate.Capture;
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

    /// <summary>
    /// Read a validated click_id off a forwarded header (default
    /// <see cref="Capture.ClickCapture.DefaultHeaderName"/>) — the decoupled path. Header lookup is
    /// case-insensitive. A forwarded header carries no <c>ref</c>, so only the click_id shape is enforced.
    /// </summary>
    public static string? GetRideBuilderClickIdFromHeader(this IHeaderDictionary headers, string name = ClickCapture.DefaultHeaderName)
    {
        if (headers is null)
            return null;

        var value = headers.TryGetValue(name, out var v) ? v.ToString() : null;
        return ClickId.IsValid(value) ? value : null;
    }

    /// <summary>
    /// Resolve a validated click_id off the live request, in the order attribution actually survives a
    /// shopper's session: the landing URL, then a forwarded header, then the
    /// <c>ridebuilder_attribution</c> cookie the browser snippet set.
    /// <para>
    /// The URL only carries the click_id on the landing hit, so URL-only resolution returns <c>null</c> on
    /// every later request (add-to-cart, checkout). The cookie is what carries it the rest of the way.
    /// Distinct from <c>context.GetRideBuilderClickId()</c>, which reads what the middleware already
    /// stashed on <see cref="HttpContext.Items"/> for THIS request.
    /// </para>
    /// </summary>
    public static string? ResolveRideBuilderClickId(this HttpRequest request)
    {
        if (request is null)
            return null;

        return request.Query.GetRideBuilderClickId()
            ?? request.Headers.GetRideBuilderClickIdFromHeader()
            ?? ClickCapture.FromCookieHeader(request.Headers.Cookie);
    }
}
