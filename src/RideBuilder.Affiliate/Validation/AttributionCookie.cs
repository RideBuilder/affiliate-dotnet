using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RideBuilder.Affiliate.Validation;

/// <summary>
/// Reads the browser attribution cookie (the client-side capture path). Mirrors
/// <c>parseAttributionCookie</c> / <c>parseCookieHeader</c> in the Node SDK (src/validation.ts) and the
/// cookie the snippet writes: <c>urlencode(JSON.stringify({ click_id, ref, clicked_at }))</c>.
/// </summary>
public static class AttributionCookie
{
    /// <summary>The cookie name the browser snippet writes.</summary>
    public const string CookieName = "ridebuilder_attribution";

    /// <summary>
    /// Parse the raw <c>ridebuilder_attribution</c> cookie value (URL-encoded JSON). Re-validates
    /// <c>ref</c> and the UUID-v4 <c>click_id</c> on read; returns <c>null</c> for a malformed or
    /// non-conforming value.
    /// </summary>
    public static Attribution? Parse(string rawValue)
    {
        try
        {
            var json = Uri.UnescapeDataString(rawValue);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var clickId = GetString(root, "click_id");
            var reff = GetString(root, "ref");
            if (ClickId.IsValid(clickId) && reff == ClickId.Ref)
                return new Attribution(clickId!, reff!, GetString(root, "clicked_at"));
        }
        catch (JsonException)
        {
            // malformed cookie
        }

        return null;
    }

    /// <summary>Split a <c>Cookie</c> header (<c>"a=1; b=2"</c>) into a name→value map (first '=' separates).</summary>
    public static IReadOnlyDictionary<string, string> ParseHeader(string cookieHeader)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in cookieHeader.Split(';'))
        {
            var i = part.IndexOf('=');
            if (i > 0)
                result[part.Substring(0, i).Trim()] = part.Substring(i + 1).Trim();
        }

        return result;
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
