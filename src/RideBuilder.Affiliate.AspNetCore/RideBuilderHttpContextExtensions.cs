using Microsoft.AspNetCore.Http;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>Read the captured click_id back off the request.</summary>
public static class RideBuilderHttpContextExtensions
{
    /// <summary>The default <see cref="HttpContext.Items"/> key the capture middleware uses.</summary>
    public const string DefaultItemKey = "RideBuilderClickId";

    /// <summary>
    /// The click_id the capture middleware stashed on this request, or <c>null</c> if none was captured.
    /// Pass <paramref name="itemKey"/> if you changed <see cref="RideBuilderCaptureOptions.ItemKey"/>.
    /// </summary>
    public static string? GetRideBuilderClickId(this HttpContext context, string itemKey = DefaultItemKey)
        => context.Items.TryGetValue(itemKey, out var value) ? value as string : null;
}
