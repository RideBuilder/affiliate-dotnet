using System.Text.RegularExpressions;

namespace RideBuilder.Affiliate.Validation;

/// <summary>
/// Shared attribution constants and click_id validation — the same rules the browser snippet and the
/// Node reference SDK (src/validation.ts) enforce. A click_id is validated against the strict UUID-v4
/// shape, NOT <see cref="System.Guid"/> parsing (which is lenient): the server accepts any GUID, but the
/// SDK and the browser snippet are strict, by design.
/// </summary>
public static class ClickId
{
    /// <summary>The required <c>ref</c> value on every affiliate link, cookie, and postback.</summary>
    public const string Ref = "ridebuilder";

    /// <summary>The UUID-v4 shape the browser snippet enforces before writing the attribution cookie.</summary>
    public static readonly Regex Pattern = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>True when <paramref name="value"/> is a valid RideBuilder click_id (UUID v4).</summary>
    public static bool IsValid(string? value) => value is not null && Pattern.IsMatch(value);
}
