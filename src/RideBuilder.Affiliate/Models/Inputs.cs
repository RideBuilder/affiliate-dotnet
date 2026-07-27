namespace RideBuilder.Affiliate;

/// <summary>Input to <see cref="RideBuilderClient.ReportCheckoutAsync"/>. Mirrors Node <c>CheckoutInput</c>.</summary>
/// <param name="OrderId">Your order id — the server-side idempotency key; a retried checkout never double-counts.</param>
/// <param name="Subtotal">Order subtotal in <b>major units</b> (e.g. <c>199.99m</c>); must be &gt; 0 with at most 2 decimal places.</param>
/// <param name="Currency">3-letter uppercase ISO-4217 code (e.g. <c>"USD"</c>).</param>
/// <param name="ClickId">The captured click_id (UUID v4).</param>
public sealed record CheckoutInput(string OrderId, decimal Subtotal, string Currency, string ClickId);

/// <summary>Input to <see cref="RideBuilderClient.ReportReturnAsync"/>. Mirrors Node <c>ReturnInput</c>.</summary>
/// <param name="ReturnId">Your return/refund id — the server-side idempotency key; a retried refund never double-counts.</param>
/// <param name="OrderId">The order id the refund references.</param>
/// <param name="RefundAmount">Refund amount in <b>major units</b>; must be &gt; 0 with at most 2 decimal places.</param>
/// <param name="Currency">3-letter uppercase ISO-4217 code; the server requires it to match the order's currency.</param>
public sealed record ReturnInput(string ReturnId, string OrderId, decimal RefundAmount, string Currency);
