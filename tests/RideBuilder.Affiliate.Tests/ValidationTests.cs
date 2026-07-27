using System;
using RideBuilder.Affiliate.Validation;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

public class ValidationTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    [Theory]
    [InlineData("1e8e6c0a-1111-4111-8111-111111111111", true)]
    [InlineData("1E8E6C0A-1111-4111-8111-111111111111", true)] // case-insensitive
    [InlineData("1e8e6c0a-1111-1111-8111-111111111111", false)] // not version 4
    [InlineData("1e8e6c0a-1111-4111-c111-111111111111", false)] // bad variant nibble
    [InlineData("not-a-uuid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_matches_the_snippet_uuid_v4_rule(string? value, bool expected)
        => Assert.Equal(expected, ClickId.IsValid(value));

    [Fact]
    public void Parse_reads_a_conforming_cookie_value()
    {
        var raw = Uri.EscapeDataString($"{{\"click_id\":\"{ValidClickId}\",\"ref\":\"ridebuilder\",\"clicked_at\":\"2026-07-23T00:00:00.000Z\"}}");

        var attribution = AttributionCookie.Parse(raw);

        Assert.NotNull(attribution);
        Assert.Equal(ValidClickId, attribution!.ClickId);
        Assert.Equal("ridebuilder", attribution.Ref);
        Assert.Equal("2026-07-23T00:00:00.000Z", attribution.ClickedAt);
    }

    [Theory]
    [InlineData("{\"click_id\":\"not-a-uuid\",\"ref\":\"ridebuilder\"}")]  // bad click_id
    [InlineData("{\"click_id\":\"1e8e6c0a-1111-4111-8111-111111111111\",\"ref\":\"other\"}")] // wrong ref
    [InlineData("not json")]
    public void Parse_rejects_non_conforming_values(string json)
        => Assert.Null(AttributionCookie.Parse(Uri.EscapeDataString(json)));

    [Fact]
    public void ParseHeader_splits_on_first_equals()
    {
        var map = AttributionCookie.ParseHeader("a=1; ridebuilder_attribution=x=y; b=2");

        Assert.Equal("1", map["a"]);
        Assert.Equal("x=y", map["ridebuilder_attribution"]);
        Assert.Equal("2", map["b"]);
    }
}
