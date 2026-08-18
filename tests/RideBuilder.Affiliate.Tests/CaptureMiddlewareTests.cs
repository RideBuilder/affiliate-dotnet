using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RideBuilder.Affiliate.AspNetCore;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

public class CaptureMiddlewareTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    private static string AttributionCookie(string clickId)
        => "ridebuilder_attribution=" + Uri.EscapeDataString(
            $"{{\"click_id\":\"{clickId}\",\"ref\":\"ridebuilder\",\"clicked_at\":\"2026-08-18T00:00:00Z\"}}");

    private static DefaultHttpContext ContextWithQuery(string query)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString(query);
        return ctx;
    }

    private static RideBuilderCaptureMiddleware Middleware(RideBuilderCaptureOptions? options = null, Action? onNext = null)
        => new(_ => { onNext?.Invoke(); return Task.CompletedTask; }, Options.Create(options ?? new RideBuilderCaptureOptions()));

    [Fact]
    public async Task Middleware_captures_click_id_and_invokes_onCapture()
    {
        var ctx = ContextWithQuery($"?ref=ridebuilder&click_id={ValidClickId}");
        string? captured = null;
        var nextCalled = false;
        var middleware = Middleware(
            new RideBuilderCaptureOptions { OnCapture = (id, _) => { captured = id; return Task.CompletedTask; } },
            () => nextCalled = true);

        await middleware.InvokeAsync(ctx);

        Assert.Equal(ValidClickId, ctx.GetRideBuilderClickId());
        Assert.Equal(ValidClickId, captured);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_does_not_capture_without_the_ref()
    {
        var ctx = ContextWithQuery($"?click_id={ValidClickId}");

        await Middleware().InvokeAsync(ctx);

        Assert.Null(ctx.GetRideBuilderClickId());
    }

    [Fact]
    public void QueryCollection_overload_reads_a_valid_click_id()
    {
        var ctx = ContextWithQuery($"?ref=ridebuilder&click_id={ValidClickId}");
        Assert.Equal(ValidClickId, ctx.Request.Query.GetRideBuilderClickId());
    }

    // The regression this suite used to miss: HttpContext.Items lives for ONE request, and only the
    // landing hit carries the click_id in its URL. A shopper who lands and then adds to cart must still
    // resolve, off the cookie the browser keeps sending — otherwise attribution is lost on every request
    // after the first and no order is ever credited.
    [Fact]
    public async Task Middleware_still_resolves_on_a_later_request_with_no_query()
    {
        var middleware = Middleware();

        var landing = ContextWithQuery($"?ref=ridebuilder&click_id={ValidClickId}");
        await middleware.InvokeAsync(landing);
        Assert.Equal(ValidClickId, landing.GetRideBuilderClickId());

        var addToCart = new DefaultHttpContext();
        addToCart.Request.Path = "/cart/add";
        addToCart.Request.Headers.Cookie = AttributionCookie(ValidClickId);
        await middleware.InvokeAsync(addToCart);

        Assert.Equal(ValidClickId, addToCart.GetRideBuilderClickId());
    }

    [Fact]
    public async Task Middleware_captures_from_the_attribution_cookie_alone()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "sid=abc; " + AttributionCookie(ValidClickId) + "; cart=1";

        await Middleware().InvokeAsync(ctx);

        Assert.Equal(ValidClickId, ctx.GetRideBuilderClickId());
    }

    [Fact]
    public async Task Middleware_captures_from_a_forwarded_header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-RideBuilder-Click-Id"] = ValidClickId;

        await Middleware().InvokeAsync(ctx);

        Assert.Equal(ValidClickId, ctx.GetRideBuilderClickId());
    }

    [Fact]
    public async Task Middleware_prefers_the_landing_url_over_a_stale_cookie()
    {
        const string FreshClickId = "2f9f7d1b-2222-4222-9222-222222222222";
        var ctx = ContextWithQuery($"?ref=ridebuilder&click_id={FreshClickId}");
        ctx.Request.Headers.Cookie = AttributionCookie(ValidClickId);

        await Middleware().InvokeAsync(ctx);

        Assert.Equal(FreshClickId, ctx.GetRideBuilderClickId());
    }

    [Fact]
    public async Task Middleware_ignores_a_malformed_cookie()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "ridebuilder_attribution=not-json";

        await Middleware().InvokeAsync(ctx);

        Assert.Null(ctx.GetRideBuilderClickId());
    }

    [Fact]
    public void ResolveRideBuilderClickId_returns_null_when_nothing_carries_it()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/cart/add";
        Assert.Null(ctx.Request.ResolveRideBuilderClickId());
    }
}
