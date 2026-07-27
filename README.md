# RideBuilder.Affiliate (.NET)

Server-side SDK for RideBuilder's FirstParty affiliate program. It does two things:

1. **Capture** the `click_id` a shopper arrives with, so your backend can bind it to the cart/order.
2. **Report** checkout and return postbacks to RideBuilder (auth, retries, idempotency handled).

Mirrors the Node reference SDK (`@ridebuilder/affiliate`) — same contract, verified by the shared
[conformance suite](../conformance). Two packages:

| Package | Target | What's in it |
|---|---|---|
| `RideBuilder.Affiliate` | `netstandard2.0` + `net8.0` | The client + framework-free capture helpers. Zero third-party dependencies (System.Text.Json only, and only on netstandard2.0). |
| `RideBuilder.Affiliate.AspNetCore` | `net8.0` | Capture middleware, `AddRideBuilder(...)` DI, and a hosted heartbeat service. |

`netstandard2.0` means the core also works on .NET Framework 4.6.2+.

## The pattern: capture at landing, bind to the order

The `click_id` only reliably reaches checkout if you take it out of the browser early and put it on the
cart/order. Capture it from the URL on landing, store it with the cart, and send it at purchase.

### ASP.NET Core

```csharp
// Program.cs
builder.Services.AddRideBuilder(o => o.ApiKey = builder.Configuration["RideBuilder:ApiKey"]!);

var app = builder.Build();
app.UseRideBuilderCapture();   // stashes a validated click_id on HttpContext.Items

// when the shopper adds to cart, persist it onto YOUR cart record:
var clickId = httpContext.GetRideBuilderClickId();
if (clickId is not null) cart.RideBuilderClickId = clickId;
```

At order time, send the postback from your backend:

```csharp
public class CheckoutService(RideBuilderClient rb)
{
    public Task ConfirmAsync(Order order) => rb.ReportCheckoutAsync(new CheckoutInput(
        OrderId: order.Id,
        Subtotal: order.Subtotal,   // major units, e.g. 199.99m
        Currency: "USD",
        ClickId: order.RideBuilderClickId));
}
```

Store the API key server-side (config/secrets) — never in frontend code.

### Console / worker (no ASP.NET)

```csharp
using var rb = new RideBuilderClient(new RideBuilderClientOptions { ApiKey = apiKey });
await rb.ReportCheckoutAsync(new CheckoutInput(order.Id, order.Subtotal, "USD", clickId));
```

### Refunds

```csharp
await rb.ReportReturnAsync(new ReturnInput(refund.Id, order.Id, refund.Amount, "USD"));
```

### Capturing without middleware

```csharp
using RideBuilder.Affiliate.Capture;

var clickId = ClickCapture.FromUrl(landingUrl)                 // from an absolute/relative URL
           ?? ClickCapture.FromCookieHeader(request.Headers.Cookie);  // recover from the browser cookie
```

## Integration protocol (register / verify / heartbeat)

```csharp
var rb = new RideBuilderClient(new RideBuilderClientOptions
{
    ApiKey = apiKey,
    Environment = RideBuilderEnvironment.Production,   // or Sandbox for test traffic
});

var (integrationId, status) = await rb.RegisterAsync();  // handshake on install/startup
await rb.VerifyAsync();                                   // deploy/CI self-test — throws on a bad key
await rb.HeartbeatAsync();                                // periodic liveness
```

On a **long-running host**, let the SDK heartbeat for you:

```csharp
builder.Services.AddRideBuilder(o => o.ApiKey = key).AddHeartbeat();   // hosted service, hourly
```

For a console/worker: `rb.StartHeartbeat();` … `rb.StopHeartbeat();`. In **serverless**, schedule
`HeartbeatAsync()` from an external trigger instead.

## Client options

`new RideBuilderClient(new RideBuilderClientOptions { ApiKey, BaseUrl?, MaxRetries?, Timeout?, Environment? })`

- `BaseUrl` — defaults to `https://api.ridebuilder.com/v1`.
- `MaxRetries` — retries on network errors, timeouts, 5xx, and 429 (default 3). Safe: `OrderId`/`ReturnId`
  are idempotency keys server-side, so nothing double-counts.
- `Timeout` — per-attempt timeout (default 10s).

`ReportCheckoutAsync` / `ReportReturnAsync` return `PostbackResult(Accepted, Status)` (`202` = accepted). A
`202` means **received, not yet validated** — RideBuilder validates asynchronously. Invalid input throws a
non-retryable `RideBuilderException`; auth/size failures (`401`, `413`) throw with `.Status` and `.ErrorCode`.
The SDK also rejects amounts with more than 2 decimal places up front, so an over-precise amount fails at
the call site instead of being silently dropped server-side.

## Contract

Wraps the RideBuilder affiliate REST contract this SDK targets — `POST /v1/postback/checkout`,
`POST /v1/postback/return`, `POST /v1/postback/health`, the `/integration/*` endpoints, the `/redirect`
link format, and API-key provisioning. A `202` from a postback means **received, not yet validated** —
RideBuilder validates asynchronously.
