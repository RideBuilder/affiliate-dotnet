using System;
using RideBuilder.Affiliate.Capture;
using RideBuilder.Affiliate.Validation;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

public class CaptureTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    [Fact]
    public void FromUrl_reads_click_id_from_an_absolute_url()
        => Assert.Equal(ValidClickId, ClickCapture.FromUrl($"https://shop.example.com/landing?ref=ridebuilder&click_id={ValidClickId}"));

    [Fact]
    public void FromUrl_reads_click_id_from_a_relative_url()
        => Assert.Equal(ValidClickId, ClickCapture.FromUrl($"/landing?ref=ridebuilder&click_id={ValidClickId}"));

    [Fact]
    public void FromUrl_returns_null_without_the_ref()
        => Assert.Null(ClickCapture.FromUrl($"/landing?click_id={ValidClickId}"));

    [Fact]
    public void FromQueryString_returns_null_for_an_invalid_click_id()
        => Assert.Null(ClickCapture.FromQueryString("?ref=ridebuilder&click_id=not-a-uuid"));

    [Fact]
    public void FromCookieHeader_recovers_click_id_from_the_attribution_cookie()
    {
        var value = Uri.EscapeDataString($"{{\"click_id\":\"{ValidClickId}\",\"ref\":\"ridebuilder\"}}");
        var header = $"session=abc; {AttributionCookie.CookieName}={value}; other=1";

        Assert.Equal(ValidClickId, ClickCapture.FromCookieHeader(header));
    }

    [Fact]
    public void FromCookieHeader_returns_null_when_the_cookie_is_absent()
        => Assert.Null(ClickCapture.FromCookieHeader("session=abc; other=1"));
}
