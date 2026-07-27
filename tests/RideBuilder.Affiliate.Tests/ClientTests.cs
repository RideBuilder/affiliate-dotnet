using System;
using System.Linq;
using System.Threading.Tasks;
using RideBuilder.Affiliate.TestSupport;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

// Mirrors sdk/node/test/health.test.mjs and integration-protocol.test.mjs.
public class ClientTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    private static RideBuilderClientOptions Options(int maxRetries = 0, RideBuilderEnvironment env = RideBuilderEnvironment.Production)
        => new() { ApiKey = "test-key", MaxRetries = maxRetries, Environment = env };

    [Fact]
    public async Task HealthCheck_resolves_ok_on_200()
    {
        var handler = new RecordingHandler((200, null));
        using var client = TestClient.Create(Options(), handler);

        var result = await client.HealthCheckAsync();

        Assert.Equal(new HealthResult(true, 200), result);
        Assert.Equal("POST", handler.Requests[0].Method);
        Assert.EndsWith("/postback/health", handler.Requests[0].Path);
        Assert.Equal("Bearer test-key", handler.Requests[0].Authorization);
    }

    [Fact]
    public async Task HealthCheck_throws_terminal_error_on_401()
    {
        var handler = new RecordingHandler((401, "{\"error\":\"invalid_auth\"}"));
        using var client = TestClient.Create(Options(), handler);

        var ex = await Assert.ThrowsAsync<RideBuilderException>(() => client.HealthCheckAsync());
        Assert.Equal(401, ex.Status);
        Assert.Equal("invalid_auth", ex.ErrorCode);
        Assert.False(ex.Retryable);
        Assert.Single(handler.Requests); // no retry on a terminal 4xx
    }

    [Fact]
    public async Task Register_posts_type_environment_capabilities_and_returns_the_id()
    {
        var handler = new RecordingHandler((200, "{\"integrationId\":\"int_abc\",\"status\":\"connected\"}"));
        using var client = TestClient.Create(Options(env: RideBuilderEnvironment.Sandbox), handler);

        var result = await client.RegisterAsync();

        Assert.Equal(new RegisterResult("int_abc", "connected"), result);
        var req = handler.Requests[0];
        Assert.Equal("POST", req.Method);
        Assert.EndsWith("/integration/register", req.Path);
        Assert.Contains("\"type\":\"dotnet_sdk\"", req.Body);
        Assert.Contains("\"environment\":\"sandbox\"", req.Body);
        Assert.Contains("\"capabilities\":[", req.Body);
    }

    [Fact]
    public async Task Verify_gets_and_resolves_ok_on_200()
    {
        var handler = new RecordingHandler((200, "{\"status\":\"connected\",\"retailerId\":\"r1\"}"));
        using var client = TestClient.Create(Options(), handler);

        var result = await client.VerifyAsync();

        Assert.Equal(new HealthResult(true, 200), result);
        Assert.Equal("GET", handler.Requests[0].Method);
        Assert.EndsWith("/integration/verify", handler.Requests[0].Path);
    }

    [Fact]
    public async Task Heartbeat_posts_type_to_integration_heartbeat()
    {
        var handler = new RecordingHandler((200, "{\"ok\":true}"));
        using var client = TestClient.Create(Options(), handler);

        var result = await client.HeartbeatAsync();

        Assert.Equal(new HealthResult(true, 200), result);
        Assert.Equal("POST", handler.Requests[0].Method);
        Assert.EndsWith("/integration/heartbeat", handler.Requests[0].Path);
        Assert.Contains("\"type\":\"dotnet_sdk\"", handler.Requests[0].Body);
    }

    [Fact]
    public async Task StartHeartbeat_fires_an_immediate_beat_then_StopHeartbeat_clears_the_timer()
    {
        var handler = new RecordingHandler((200, "{\"ok\":true}"));
        using var client = TestClient.Create(Options(), handler);

        client.StartHeartbeat(TimeSpan.FromSeconds(30)); // interval far longer than the wait: only the immediate beat fires
        await Task.Delay(250);
        client.StopHeartbeat();

        var beats = handler.Requests.Count(r => r.Path.EndsWith("/integration/heartbeat", StringComparison.Ordinal));
        Assert.True(beats >= 1, "expected at least the immediate heartbeat");
    }

    [Fact]
    public async Task ReportCheckout_retries_on_500_then_succeeds()
    {
        var handler = new RecordingHandler((500, null), (202, null));
        using var client = TestClient.Create(Options(maxRetries: 3), handler);

        var result = await client.ReportCheckoutAsync(new CheckoutInput("ORDER-1", 199.99m, "USD", ValidClickId));

        Assert.True(result.Accepted);
        Assert.Equal(202, result.Status);
        Assert.Equal(2, handler.Requests.Count);
    }
}
