using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RideBuilder.Affiliate.Internal;

// Wire DTOs. Request bodies are snake_case (postbacks) / lowercase single-words (integration), so every
// property carries an explicit [JsonPropertyName] — do NOT rely on a naming policy.

internal sealed record CheckoutBody(
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("order_subtotal")] decimal OrderSubtotal,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("click_id")] string ClickId);

internal sealed record ReturnBody(
    [property: JsonPropertyName("return_id")] string ReturnId,
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("refund_amount")] decimal RefundAmount,
    [property: JsonPropertyName("currency")] string Currency);

internal sealed record RegisterBody(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities);

internal sealed record HeartbeatBody(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("version")] string Version);
