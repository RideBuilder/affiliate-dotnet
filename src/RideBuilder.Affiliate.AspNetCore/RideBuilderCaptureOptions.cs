using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>Options for the click_id capture middleware. Mirrors the Node <c>CaptureOptions</c>.</summary>
public sealed class RideBuilderCaptureOptions
{
    /// <summary>
    /// The <see cref="HttpContext.Items"/> key the captured click_id is stashed under
    /// (default <c>"RideBuilderClickId"</c>). Read it via <c>context.GetRideBuilderClickId()</c>.
    /// </summary>
    public string ItemKey { get; set; } = RideBuilderHttpContextExtensions.DefaultItemKey;

    /// <summary>
    /// Optional callback invoked when a click_id is captured — persist it onto your cart/order here.
    /// Runs before the next middleware; exceptions are swallowed so persistence never breaks the request.
    /// </summary>
    public Func<string, HttpContext, Task>? OnCapture { get; set; }
}
