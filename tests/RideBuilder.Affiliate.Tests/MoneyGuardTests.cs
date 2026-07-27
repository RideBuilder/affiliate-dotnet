using System.Threading.Tasks;
using RideBuilder.Affiliate.TestSupport;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

// The .NET SDK strengthens Node's positive-amount check: it also rejects amounts with more than 2 decimal
// places (matching the server's MoneyMath.DollarsToCentsOrNull), so an over-precise amount fails locally
// instead of being silently dropped by the async poller.
public class MoneyGuardTests
{
    private const string ValidClickId = "1e8e6c0a-1111-4111-8111-111111111111";

    private static RideBuilderClientOptions Options => new() { ApiKey = "test-key", MaxRetries = 0 };

    [Theory]
    [InlineData("1.999")]  // more than 2 decimal places
    [InlineData("0")]      // not positive
    [InlineData("-5")]     // negative
    public async Task ReportCheckout_rejects_bad_subtotal_before_any_request(string subtotal)
    {
        var handler = new RecordingHandler((202, null));
        using var client = TestClient.Create(Options, handler);

        await Assert.ThrowsAsync<RideBuilderException>(() =>
            client.ReportCheckoutAsync(new CheckoutInput("ORDER-1", decimal.Parse(subtotal, System.Globalization.CultureInfo.InvariantCulture), "USD", ValidClickId)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReportReturn_rejects_over_precise_refund_before_any_request()
    {
        var handler = new RecordingHandler((202, null));
        using var client = TestClient.Create(Options, handler);

        await Assert.ThrowsAsync<RideBuilderException>(() =>
            client.ReportReturnAsync(new ReturnInput("RET-1", "ORDER-1", 10.001m, "USD")));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReportCheckout_rejects_bad_currency_before_any_request()
    {
        var handler = new RecordingHandler((202, null));
        using var client = TestClient.Create(Options, handler);

        await Assert.ThrowsAsync<RideBuilderException>(() =>
            client.ReportCheckoutAsync(new CheckoutInput("ORDER-1", 199.99m, "usd", ValidClickId)));

        Assert.Empty(handler.Requests);
    }
}
