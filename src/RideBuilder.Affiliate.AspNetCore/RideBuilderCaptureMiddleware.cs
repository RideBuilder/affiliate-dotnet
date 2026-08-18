using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>
/// Reads a validated click_id off each incoming request and stashes it on
/// <c>HttpContext.Items["RideBuilderClickId"]</c>. Resolves in the order attribution survives — landing
/// URL, forwarded header, then the <c>ridebuilder_attribution</c> cookie — so a returning shopper is
/// re-hydrated on requests whose URL carries nothing.
/// <para>
/// <c>Items</c> lives for ONE request. Bind the click_id onto your cart/order from
/// <see cref="RideBuilderCaptureOptions.OnCapture"/>, or read it with
/// <c>context.GetRideBuilderClickId()</c> during the same request — never expect it to survive to the next
/// one. Send the value stored on your own order at checkout.
/// </para>
/// </summary>
public sealed class RideBuilderCaptureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RideBuilderCaptureOptions _options;

    /// <summary>Create the middleware.</summary>
    public RideBuilderCaptureMiddleware(RequestDelegate next, IOptions<RideBuilderCaptureOptions> options)
    {
        _next = next;
        _options = options?.Value ?? new RideBuilderCaptureOptions();
    }

    /// <summary>Invoke the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var clickId = context.Request.ResolveRideBuilderClickId();
        if (clickId is not null)
        {
            context.Items[_options.ItemKey] = clickId;
            if (_options.OnCapture is not null)
            {
                try
                {
                    // Awaited (so HttpContext stays valid) but swallowed — persistence errors from the
                    // caller must never break the request.
                    await _options.OnCapture(clickId, context).ConfigureAwait(false);
                }
                catch
                {
                    // caller's persistence concern
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
