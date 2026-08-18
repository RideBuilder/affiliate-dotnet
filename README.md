# RideBuilder.Affiliate (.NET)

Server-side SDK for RideBuilder's FirstParty affiliate program. It does two things:

1. **Capture** the `click_id` a shopper arrives with, so your backend can bind it to the cart/order.
2. **Report** checkout and return postbacks to RideBuilder (auth, retries, idempotency handled).

Mirrors the Node reference SDK (`@ridebuilder/affiliate`) — same contract, verified by the shared
[conformance suite](./conformance). Two packages:

| Package | Target | What's in it |
|---|---|---|
| `RideBuilder.Affiliate` | `netstandard2.0` + `net462` + `net8.0` | The client + framework-free capture helpers. Zero third-party dependencies (System.Text.Json only, and only off `net8.0`). |
| `RideBuilder.Affiliate.AspNetCore` | `net8.0` | Capture middleware, `AddRideBuilder(...)` DI, and a hosted heartbeat service. |

## Install

```sh
dotnet add package RideBuilder.Affiliate
```

On ASP.NET Core, add the integration package too — it brings the core package with it:

```sh
dotnet add package RideBuilder.Affiliate.AspNetCore
```

On **.NET Framework 4.6.2+**, `dotnet add package RideBuilder.Affiliate` is all you need; the package
carries the `System.Net.Http` framework reference itself. See [.NET Framework](#net-framework) below for
the capture pattern there (the ASP.NET Core package is `net8.0`-only).

## The pattern: capture at landing, bind to the order

The `click_id` only reliably reaches checkout if you take it out of the browser early and put it on the
cart/order. **Capture it and persist it in the same breath** — then send what you stored at purchase.

### ASP.NET Core

```csharp
// Program.cs
builder.Services.AddRideBuilder(o => o.ApiKey = builder.Configuration["RideBuilder:ApiKey"]!);

// Bind it onto YOUR cart the moment it's seen. OnCapture runs on the request that carried it.
builder.Services.ConfigureRideBuilderCapture(o => o.OnCapture = async (clickId, ctx) =>
{
    var cart = await carts.GetOrCreateAsync(ctx);
    cart.RideBuilderClickId = clickId;     // your storage — this is what survives to checkout
    await carts.SaveAsync(cart);
});

var app = builder.Build();
app.UseRideBuilderCapture();   // place it early, before your handlers
```

The middleware resolves a validated click_id in the order attribution actually survives:

1. the **landing URL** (`?ref=ridebuilder&click_id=…`) — present only on the first hit,
2. a forwarded **`X-RideBuilder-Click-Id` header** — the decoupled-frontend path,
3. the **`ridebuilder_attribution` cookie** the browser snippet set — sent on every later request.

> **`HttpContext.Items` lives for one request.** `context.GetRideBuilderClickId()` returns what was
> resolved on *that* request — it is not a session store. Persist the value onto your own cart/order
> (as above) and read it back from there at checkout. Don't capture on the landing request and expect
> `Items` to still hold it when the shopper adds to cart.

At order time, send what you stored:

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

## Decoupled frontend (e.g. React) + separate .NET backend

If your frontend is separate from your API, the landing request never hits your backend. The browser
**snippet** captures the `click_id` into a first-party cookie; get it to your backend one of two ways:

```csharp
using RideBuilder.Affiliate.Capture;

// Same registrable domain — the cookie rides along; read it off the Cookie header:
var clickId = ClickCapture.FromCookieHeader(request.Headers.Cookie);

// Cross-domain / mobile — the frontend forwards it on the checkout call:
var clickId = ClickCapture.FromHeaders(headers);   // default header: X-RideBuilder-Click-Id
```

Either way `ReportCheckoutAsync` is unchanged. On ASP.NET Core, `UseRideBuilderCapture()` already checks
both, or call `context.Request.ResolveRideBuilderClickId()` yourself to run the same chain on demand.

## .NET Framework

The core package supports **.NET Framework 4.6.2+** via its `net462` target — no manual assembly
references required. The ASP.NET Core package targets `net8.0`, so on ASP.NET / MVC5 / WebForms use the
framework-free helpers directly:

```csharp
using RideBuilder.Affiliate.Capture;

// in Application_BeginRequest, a base controller, or an IHttpModule:
var clickId = ClickCapture.FromUrl(Request.RawUrl)
           ?? ClickCapture.FromHeaders(ToDictionary(Request.Headers))
           ?? ClickCapture.FromCookieHeader(Request.Headers["Cookie"]);

if (clickId != null) cart.RideBuilderClickId = clickId;   // persist it, same as above
```

## Capture helpers

All validate `ref == "ridebuilder"` and the UUID-v4 `click_id`, returning `null` otherwise — the same
rules the browser snippet enforces. None of them persist anything; binding is yours.

- `ClickCapture.FromUrl(url)` / `FromQuery(map)` / `FromQueryString(qs)` — landing capture.
- `ClickCapture.FromCookieHeader(cookieHeader)` — recover it from the `ridebuilder_attribution` cookie.
- `ClickCapture.FromHeaders(headers, name = "X-RideBuilder-Click-Id")` — a forwarded header
  (case-insensitive; carries no `ref`, so only the click_id shape is checked).
- ASP.NET Core: `request.ResolveRideBuilderClickId()` runs all three in order;
  `context.GetRideBuilderClickId()` reads what the middleware resolved for the current request.

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

This SDK wraps the RideBuilder affiliate REST contract — `POST /v1/postback/checkout`,
`POST /v1/postback/return`, `POST /v1/postback/health`, the `/integration/*` endpoints, the `/redirect`
link format, and API-key provisioning. A `202` from a postback means **received, not yet validated** —
RideBuilder validates asynchronously.
