using System;
using System.Collections.Generic;
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
    public void FromHeaders_reads_the_forwarded_header()
        => Assert.Equal(ValidClickId, ClickCapture.FromHeaders(
            new Dictionary<string, string?> { ["X-RideBuilder-Click-Id"] = ValidClickId }));

    [Fact]
    public void FromHeaders_matches_the_header_name_case_insensitively()
        => Assert.Equal(ValidClickId, ClickCapture.FromHeaders(
            new Dictionary<string, string?> { ["x-ridebuilder-click-id"] = ValidClickId }));

    [Fact]
    public void FromHeaders_honours_a_custom_header_name()
        => Assert.Equal(ValidClickId, ClickCapture.FromHeaders(
            new Dictionary<string, string?> { ["X-Shop-Click"] = ValidClickId }, "X-Shop-Click"));

    [Fact]
    public void FromHeaders_returns_null_for_an_invalid_click_id()
        => Assert.Null(ClickCapture.FromHeaders(
            new Dictionary<string, string?> { ["X-RideBuilder-Click-Id"] = "not-a-uuid" }));

    [Fact]
    public void FromHeaders_returns_null_when_the_header_is_absent()
        => Assert.Null(ClickCapture.FromHeaders(new Dictionary<string, string?> { ["Accept"] = "*/*" }));

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
