using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RideBuilder.Affiliate;
using RideBuilder.Affiliate.TestSupport;
using Xunit;

namespace RideBuilder.Affiliate.Conformance;

/// <summary>
/// Runs the shared, language-neutral fixtures in <c>sdk/conformance/*.json</c> against the .NET client — the
/// same cases the Node SDK runs — so both encode byte-for-byte the same wire contract.
/// </summary>
public class ConformanceTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEnumerable<object[]> Cases()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "conformance");
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var name = element.GetProperty("name").GetString()!;
                yield return new object[] { name, element.GetRawText() };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Conformance(string name, string caseJson)
    {
        _ = name;
        using var doc = JsonDocument.Parse(caseJson);
        var c = doc.RootElement;

        var handler = new RecordingHandler(BuildResponses(c));
        using var client = TestClient.Create(BuildOptions(c.GetProperty("client")), handler);

        RideBuilderException? error = null;
        JsonElement? result = null;
        try
        {
            result = await InvokeAsync(client, c.GetProperty("call").GetString()!, c.GetProperty("args"));
        }
        catch (RideBuilderException ex)
        {
            error = ex;
        }

        var expect = c.GetProperty("expect");

        if (GetBool(expect, "argError"))
        {
            Assert.True(error is not null, "expected the call to throw on bad input");
            AssertRequestCount(expect, handler);
            return;
        }

        if (expect.TryGetProperty("request", out var request))
            AssertRequest(request, handler);
        AssertRequestCount(expect, handler);

        if (expect.TryGetProperty("error", out var expectedError))
        {
            Assert.True(error is not null, $"expected an error but the call succeeded ({name})");
            if (expectedError.TryGetProperty("retryable", out var r))
                Assert.Equal(r.GetBoolean(), error!.Retryable);
            if (expectedError.TryGetProperty("status", out var s))
                Assert.Equal(s.GetInt32(), error!.Status);
            if (expectedError.TryGetProperty("errorCode", out var ec))
                Assert.Equal(ec.GetString(), error!.ErrorCode);
            return;
        }

        Assert.True(error is null, $"unexpected error: {error?.Message}");
        if (expect.TryGetProperty("result", out var expectedResult))
            Assert.True(JsonMatch(expectedResult, result!.Value), $"result mismatch for '{name}'");
    }

    private async Task<JsonElement> InvokeAsync(RideBuilderClient client, string call, JsonElement args)
    {
        switch (call)
        {
            case "reportCheckout":
                return ToElement(await client.ReportCheckoutAsync(new CheckoutInput(
                    args.GetProperty("orderId").GetString()!,
                    GetDecimal(args, "subtotal"),
                    args.GetProperty("currency").GetString()!,
                    args.GetProperty("clickId").GetString()!)));
            case "reportReturn":
                return ToElement(await client.ReportReturnAsync(new ReturnInput(
                    args.GetProperty("returnId").GetString()!,
                    args.GetProperty("orderId").GetString()!,
                    GetDecimal(args, "refundAmount"),
                    args.GetProperty("currency").GetString()!)));
            case "healthCheck":
                return ToElement(await client.HealthCheckAsync());
            case "register":
                return ToElement(await client.RegisterAsync());
            case "verify":
                return ToElement(await client.VerifyAsync());
            case "heartbeat":
                return ToElement(await client.HeartbeatAsync());
            default:
                throw new InvalidOperationException($"unknown call: {call}");
        }
    }

    private JsonElement ToElement(object result) => JsonSerializer.SerializeToElement(result, CamelCase);

    private static (int, string?)[] BuildResponses(JsonElement c)
    {
        if (c.TryGetProperty("responses", out var arr))
            return arr.EnumerateArray().Select(ToResponse).ToArray();
        if (c.TryGetProperty("response", out var one))
            return new[] { ToResponse(one) };
        return new[] { (200, (string?)null) };
    }

    private static (int, string?) ToResponse(JsonElement r)
    {
        var status = r.GetProperty("status").GetInt32();
        var body = r.TryGetProperty("body", out var b) && b.ValueKind != JsonValueKind.Null ? b.GetRawText() : null;
        return (status, body);
    }

    private static RideBuilderClientOptions BuildOptions(JsonElement cfg)
    {
        var options = new RideBuilderClientOptions
        {
            ApiKey = cfg.GetProperty("apiKey").GetString()!,
            MaxRetries = cfg.TryGetProperty("maxRetries", out var mr) ? mr.GetInt32() : 3,
        };
        if (cfg.TryGetProperty("baseUrl", out var bu))
            options.BaseUrl = bu.GetString();
        if (cfg.TryGetProperty("environment", out var env) && env.GetString() == "sandbox")
            options.Environment = RideBuilderEnvironment.Sandbox;
        return options;
    }

    private static void AssertRequest(JsonElement expected, RecordingHandler handler)
    {
        Assert.True(handler.Requests.Count > 0, "expected at least one HTTP request");
        var actual = handler.Requests[0];

        if (expected.TryGetProperty("method", out var m))
            Assert.Equal(m.GetString(), actual.Method);
        if (expected.TryGetProperty("path", out var p))
            Assert.EndsWith(p.GetString()!, actual.Path);
        if (expected.TryGetProperty("headers", out var headers))
        {
            foreach (var h in headers.EnumerateObject())
            {
                if (string.Equals(h.Name, "authorization", StringComparison.OrdinalIgnoreCase))
                    Assert.Equal(h.Value.GetString(), actual.Authorization);
            }
        }
        if (expected.TryGetProperty("body", out var body))
        {
            Assert.NotNull(actual.Body);
            using var actualDoc = JsonDocument.Parse(actual.Body!);
            Assert.True(JsonMatch(body, actualDoc.RootElement), $"request body mismatch; sent {actual.Body}");
        }
    }

    private static void AssertRequestCount(JsonElement expect, RecordingHandler handler)
    {
        if (expect.TryGetProperty("requestCount", out var rc))
            Assert.Equal(rc.GetInt32(), handler.Requests.Count);
    }

    // Deep JSON match. Numbers compare by value; a string expected value wrapped in <angle brackets> is a
    // wildcard requiring any non-empty string; objects must have the same key set.
    private static bool JsonMatch(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.String)
        {
            var s = expected.GetString()!;
            if (s.Length >= 2 && s[0] == '<' && s[^1] == '>')
                return actual.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(actual.GetString());
        }

        if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
            return expected.GetDecimal() == actual.GetDecimal();

        if (expected.ValueKind != actual.ValueKind)
            return false;

        switch (expected.ValueKind)
        {
            case JsonValueKind.String:
                return expected.GetString() == actual.GetString();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.Object:
                var expectedProps = expected.EnumerateObject().ToList();
                var actualCount = actual.EnumerateObject().Count();
                if (expectedProps.Count != actualCount)
                    return false;
                foreach (var prop in expectedProps)
                {
                    if (!actual.TryGetProperty(prop.Name, out var actualValue) || !JsonMatch(prop.Value, actualValue))
                        return false;
                }
                return true;
            case JsonValueKind.Array:
                var e = expected.EnumerateArray().ToList();
                var a = actual.EnumerateArray().ToList();
                if (e.Count != a.Count)
                    return false;
                return !e.Where((t, i) => !JsonMatch(t, a[i])).Any();
            default:
                return false;
        }
    }

    private static bool GetBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static decimal GetDecimal(JsonElement obj, string name)
    {
        var v = obj.GetProperty(name);
        return v.ValueKind == JsonValueKind.String
            ? decimal.Parse(v.GetString()!, CultureInfo.InvariantCulture)
            : v.GetDecimal();
    }
}
