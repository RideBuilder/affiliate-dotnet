using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RideBuilder.Affiliate.AspNetCore;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

public class CaptureMiddlewareTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    private static DefaultHttpContext ContextWithQuery(string query)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString(query);
        return ctx;
    }

    [Fact]
    public async Task Middleware_captures_click_id_and_invokes_onCapture()
    {
        var ctx = ContextWithQuery($"?ref=ridebuilder&click_id={ValidClickId}");
        string? captured = null;
        var options = Options.Create(new RideBuilderCaptureOptions
        {
            OnCapture = (id, _) => { captured = id; return Task.CompletedTask; },
        });
        var nextCalled = false;
        var middleware = new RideBuilderCaptureMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, options);

        await middleware.InvokeAsync(ctx);

        Assert.Equal(ValidClickId, ctx.GetRideBuilderClickId());
        Assert.Equal(ValidClickId, captured);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_does_not_capture_without_the_ref()
    {
        var ctx = ContextWithQuery($"?click_id={ValidClickId}");
        var middleware = new RideBuilderCaptureMiddleware(_ => Task.CompletedTask, Options.Create(new RideBuilderCaptureOptions()));

        await middleware.InvokeAsync(ctx);

        Assert.Null(ctx.GetRideBuilderClickId());
    }

    [Fact]
    public void QueryCollection_overload_reads_a_valid_click_id()
    {
        var ctx = ContextWithQuery($"?ref=ridebuilder&click_id={ValidClickId}");
        Assert.Equal(ValidClickId, ctx.Request.Query.GetRideBuilderClickId());
    }
}
