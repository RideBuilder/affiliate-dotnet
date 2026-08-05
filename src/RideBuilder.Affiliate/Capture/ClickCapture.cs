using System;
using System.Collections.Generic;
using RideBuilder.Affiliate.Validation;

namespace RideBuilder.Affiliate.Capture;

/// <summary>
/// Framework-free landing-capture helpers — read a validated <c>click_id</c> off an incoming request.
/// Mirrors the helpers in the Node SDK (src/capture.ts). All helpers validate <c>ref == "ridebuilder"</c>
/// and the UUID-v4 <c>click_id</c>, returning <c>null</c> otherwise (the same rules the browser snippet
/// enforces). None of these persist the id — bind it onto your cart/order yourself.
/// <para>
/// ASP.NET Core middleware and the <c>IQueryCollection</c> overload live in the
/// <c>RideBuilder.Affiliate.AspNetCore</c> package so this core stays dependency-free.
/// </para>
/// </summary>
public static class ClickCapture
{
    /// <summary>Read the click_id from a decoded query map (e.g. framework query parameters).</summary>
    public static string? FromQuery(IReadOnlyDictionary<string, string?> query)
    {
        if (query is null)
            return null;
        query.TryGetValue("ref", out var reff);
        if (reff != ClickId.Ref)
            return null;
        query.TryGetValue("click_id", out var clickId);
        return ClickId.IsValid(clickId) ? clickId : null;
    }

    /// <summary>Read the click_id from a raw URL (absolute, or relative like <c>/landing?...</c>).</summary>
    public static string? FromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        // Treat it as absolute only when it truly has an http(s) scheme. On Unix, Uri.TryCreate with
        // UriKind.Absolute also accepts a leading-slash path as a file:// URI (which drops the query), so
        // UriKind.Absolute alone can't tell absolute from relative — resolve relatives against a placeholder
        // base to read their query, mirroring getClickIdFromUrl in the Node SDK.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            var rel = url[0] == '/' ? url : "/" + url;
            if (!Uri.TryCreate("http://placeholder.local" + rel, UriKind.Absolute, out parsed))
                return null;
        }

        return FromQueryString(parsed.Query);
    }

    /// <summary>Read the click_id from a raw query string (<c>"?a=b&amp;c=d"</c> or <c>"a=b&amp;c=d"</c>).</summary>
    public static string? FromQueryString(string? queryString)
        => FromQuery(ParseQueryString(queryString));

    /// <summary>
    /// Recover the click_id from the <c>ridebuilder_attribution</c> cookie the browser snippet set — the
    /// client-side path, for same-domain checkouts. Reads the raw <c>Cookie</c> request header.
    /// </summary>
    public static string? FromCookieHeader(string? cookieHeader)
    {
        if (string.IsNullOrEmpty(cookieHeader))
            return null;

        var cookies = AttributionCookie.ParseHeader(cookieHeader!);
        if (!cookies.TryGetValue(AttributionCookie.CookieName, out var raw) || string.IsNullOrEmpty(raw))
            return null;

        return AttributionCookie.Parse(raw)?.ClickId;
    }

    // Minimal query-string parser (first value wins, like URLSearchParams.get; '+' decodes to space).
    internal static IReadOnlyDictionary<string, string?> ParseQueryString(string? queryString)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(queryString))
            return map;

        var q = queryString!;
        if (q[0] == '?')
            q = q.Substring(1);

        foreach (var pair in q.Split('&'))
        {
            if (pair.Length == 0)
                continue;

            var eq = pair.IndexOf('=');
            string key, val;
            if (eq < 0)
            {
                key = pair;
                val = string.Empty;
            }
            else
            {
                key = pair.Substring(0, eq);
                val = pair.Substring(eq + 1);
            }

            key = Uri.UnescapeDataString(key.Replace("+", " "));
            val = Uri.UnescapeDataString(val.Replace("+", " "));
            if (!map.ContainsKey(key))
                map[key] = val;
        }

        return map;
    }
}
