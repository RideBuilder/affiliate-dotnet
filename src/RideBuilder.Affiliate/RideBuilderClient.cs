using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RideBuilder.Affiliate.Internal;
using RideBuilder.Affiliate.Validation;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("RideBuilder.Affiliate.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("RideBuilder.Affiliate.Conformance")]

namespace RideBuilder.Affiliate;

/// <summary>
/// Server-side client for RideBuilder's FirstParty affiliate program: report checkout/return postbacks and
/// prove integration liveness. Handles auth, per-attempt timeout, retry with exponential backoff + jitter,
/// and idempotency. Port of the Node <c>RideBuilderClient</c> (src/client.ts).
/// </summary>
public sealed class RideBuilderClient : IDisposable
{
    private const string DefaultBaseUrl = "https://api.ridebuilder.com/v1";
    private static readonly Regex CurrencyRe = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;
    private readonly string _environment;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Func<TimeSpan, CancellationToken, Task> _sleeper;
    private readonly Random _rng = new();

    private CancellationTokenSource? _heartbeatCts;

    /// <summary>Create a client. Pass an <see cref="HttpClient"/> to reuse one (e.g. from <c>IHttpClientFactory</c> or in tests); otherwise the client owns a default one.</summary>
    public RideBuilderClient(RideBuilderClientOptions options, HttpClient? httpClient = null)
        : this(options, httpClient, sleeper: null)
    {
    }

    // Internal ctor: tests inject a no-op sleeper so retry logic runs without real delays.
    internal RideBuilderClient(RideBuilderClientOptions options, HttpClient? httpClient, Func<TimeSpan, CancellationToken, Task>? sleeper)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        AssertNonEmpty(options.ApiKey, "ApiKey");

        _apiKey = options.ApiKey;
        _baseUrl = (options.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _maxRetries = options.MaxRetries;
        _timeout = options.Timeout;
        _environment = options.Environment == RideBuilderEnvironment.Sandbox ? "sandbox" : "production";
        _http = httpClient ?? new HttpClient();
        _ownsHttp = httpClient is null;
        _sleeper = sleeper ?? ((delay, ct) => Task.Delay(delay, ct));

        // We enforce a per-attempt timeout via a linked CTS with CancelAfter (the analog of Node's
        // AbortController), so leave an owned HttpClient's own timeout unbounded to avoid double-timeout.
        if (_ownsHttp)
            _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <summary>Report a checkout. <c>OrderId</c> is the idempotency key, so a retried checkout never double-counts.</summary>
    public Task<PostbackResult> ReportCheckoutAsync(CheckoutInput input, CancellationToken cancellationToken = default)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        AssertNonEmpty(input.OrderId, "OrderId");
        AssertPositiveAmount(input.Subtotal, "Subtotal");
        AssertCurrency(input.Currency);
        if (!ClickId.IsValid(input.ClickId))
            throw new RideBuilderException("ClickId must be a valid RideBuilder click_id (UUID v4)", retryable: false);

        var body = new CheckoutBody(input.OrderId, input.Subtotal, input.Currency, ClickId.Ref, input.ClickId);
        return PostAsync("/postback/checkout", body, cancellationToken);
    }

    /// <summary>Report a return/refund. <c>ReturnId</c> is the idempotency key, so a retried refund never double-counts.</summary>
    public Task<PostbackResult> ReportReturnAsync(ReturnInput input, CancellationToken cancellationToken = default)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        AssertNonEmpty(input.ReturnId, "ReturnId");
        AssertNonEmpty(input.OrderId, "OrderId");
        AssertPositiveAmount(input.RefundAmount, "RefundAmount");
        AssertCurrency(input.Currency);

        var body = new ReturnBody(input.ReturnId, input.OrderId, input.RefundAmount, input.Currency);
        return PostAsync("/postback/return", body, cancellationToken);
    }

    /// <summary>Authenticated liveness ping (legacy alias). Proves the API key is valid + active; a bad key throws a terminal <see cref="RideBuilderException"/> with <c>ErrorCode "invalid_auth"</c>.</summary>
    public async Task<HealthResult> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        using var res = await SendAsync(HttpMethod.Post, "/postback/health", null, cancellationToken).ConfigureAwait(false);
        return new HealthResult(true, (int)res.StatusCode);
    }

    /// <summary>Announce this integration on install/startup — the handshake. Idempotent; returns the stable integration id.</summary>
    public async Task<RegisterResult> RegisterAsync(IReadOnlyList<string>? capabilities = null, CancellationToken cancellationToken = default)
    {
        var body = new RegisterBody(SdkInfo.Type, _environment, SdkInfo.Version, capabilities ?? SdkInfo.DefaultCapabilities);
        using var res = await SendAsync(HttpMethod.Post, "/integration/register", body, cancellationToken).ConfigureAwait(false);
        var (integrationId, status) = await ReadRegisterAsync(res).ConfigureAwait(false);
        return new RegisterResult(integrationId, status);
    }

    /// <summary>Self-test: verifies the API key is valid + active. No side effect. Throws a terminal <see cref="RideBuilderException"/> on a bad/rotated key.</summary>
    public async Task<HealthResult> VerifyAsync(CancellationToken cancellationToken = default)
    {
        using var res = await SendAsync(HttpMethod.Get, "/integration/verify", null, cancellationToken).ConfigureAwait(false);
        return new HealthResult(true, (int)res.StatusCode);
    }

    /// <summary>Periodic liveness. Call on a schedule so RideBuilder can tell "alive" from "went dark" independently of order volume.</summary>
    public async Task<HealthResult> HeartbeatAsync(CancellationToken cancellationToken = default)
    {
        var body = new HeartbeatBody(SdkInfo.Type, _environment, SdkInfo.Version);
        using var res = await SendAsync(HttpMethod.Post, "/integration/heartbeat", body, cancellationToken).ConfigureAwait(false);
        return new HealthResult(true, (int)res.StatusCode);
    }

    /// <summary>
    /// Opt-in auto-heartbeat for long-running processes (console/worker): fires one beat now, then every
    /// <paramref name="interval"/> (default hourly). Failed pings are swallowed. Call <see cref="StopHeartbeat"/>
    /// on shutdown. In ASP.NET hosts prefer the hosted service in <c>RideBuilder.Affiliate.AspNetCore</c>;
    /// serverless should schedule <see cref="HeartbeatAsync"/> externally.
    /// </summary>
    public void StartHeartbeat(TimeSpan? interval = null)
    {
        StopHeartbeat();
        var period = interval ?? TimeSpan.FromHours(1);
        var cts = new CancellationTokenSource();
        _heartbeatCts = cts;
        _ = RunHeartbeatLoopAsync(period, cts.Token);
    }

    /// <summary>Stop the auto-heartbeat started by <see cref="StartHeartbeat"/>.</summary>
    public void StopHeartbeat()
    {
        var cts = _heartbeatCts;
        if (cts is null)
            return;
        _heartbeatCts = null;
        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopHeartbeat();
        if (_ownsHttp)
            _http.Dispose();
    }

    private async Task RunHeartbeatLoopAsync(TimeSpan period, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await HeartbeatAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // A failed heartbeat must not surface as an unhandled exception.
            }

            try
            {
                await Task.Delay(period, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<PostbackResult> PostAsync(string path, object body, CancellationToken ct)
    {
        using var res = await SendAsync(HttpMethod.Post, path, body, ct).ConfigureAwait(false);
        return new PostbackResult(true, (int)res.StatusCode);
    }

    // Shared transport: auth header, per-attempt timeout/abort, retry with backoff on 5xx/429/network,
    // terminal throw on other 4xx. Returns the 2xx response. Mirrors send() in the Node client.
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        var url = _baseUrl + path;
        var hasBody = payload is not null;
        var bodyJson = hasBody ? JsonSerializer.Serialize(payload!, payload!.GetType(), JsonOptions) : null;
        RideBuilderException? lastError = null;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            if (attempt > 0)
                await _sleeper(Backoff(attempt), ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeout);
            try
            {
                using var req = new HttpRequestMessage(method, url);
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
                if (hasBody)
                {
                    var content = new StringContent(bodyJson!, Encoding.UTF8);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    req.Content = content;
                }

                var res = await _http.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
                var status = (int)res.StatusCode;

                if (status is >= 200 and < 300)
                    return res;

                if (status >= 500 || status == 429)
                {
                    res.Dispose();
                    lastError = new RideBuilderException($"RideBuilder request failed with {status}", retryable: true, status: status);
                    continue;
                }

                // Other 4xx (401 missing/invalid_auth, 413 payload_too_large, 400): terminal.
                var errorCode = await ReadErrorCodeAsync(res).ConfigureAwait(false);
                res.Dispose();
                throw new RideBuilderException(
                    errorCode is not null
                        ? $"RideBuilder request rejected ({status}): {errorCode}"
                        : $"RideBuilder request rejected with {status}",
                    retryable: false, status: status, errorCode: errorCode);
            }
            catch (RideBuilderException ex)
            {
                if (!ex.Retryable)
                    throw;
                lastError = ex;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Caller cancelled — propagate, do not retry.
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
            {
                // Network error or per-attempt timeout — retryable.
                lastError = new RideBuilderException(ex.Message, retryable: true);
            }
        }

        throw lastError ?? new RideBuilderException("RideBuilder request failed after retries", retryable: true);
    }

    // 250 * 2^(attempt-1) ms + up to 100ms jitter — identical to the Node backoff.
    private TimeSpan Backoff(int attempt)
        => TimeSpan.FromMilliseconds((250 * Math.Pow(2, attempt - 1)) + Jitter());

    // Random instance methods are not thread-safe. One client shared across concurrent requests (the
    // normal DI setup) could otherwise corrupt its state and collapse the jitter that keeps retries
    // from bunching up.
    private int Jitter()
    {
        lock (_rng)
            return _rng.Next(100);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage res)
    {
        try
        {
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
                return null;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                return e.GetString();
        }
        catch
        {
            // non-JSON body
        }

        return null;
    }

    private static async Task<(string IntegrationId, string Status)> ReadRegisterAsync(HttpResponseMessage res)
    {
        try
        {
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = root.TryGetProperty("integrationId", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()! : string.Empty;
            var status = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "connected";
            return (id, status);
        }
        catch
        {
            return (string.Empty, "connected");
        }
    }

    private static void AssertNonEmpty(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RideBuilderException($"{field} must be a non-empty string", retryable: false);
    }

    // Strengthened over Node's assertPositiveAmount: also rejects > 2 decimal places, matching the server's
    // MoneyMath.DollarsToCentsOrNull — so an over-precise amount fails here instead of being silently dropped.
    private static void AssertPositiveAmount(decimal value, string field)
    {
        if (value <= 0m)
            throw new RideBuilderException($"{field} must be a positive number", retryable: false);
        if (value != decimal.Round(value, 2, MidpointRounding.ToEven))
            throw new RideBuilderException($"{field} must have at most 2 decimal places", retryable: false);
    }

    private static void AssertCurrency(string value)
    {
        if (value is null || !CurrencyRe.IsMatch(value))
            throw new RideBuilderException("currency must be a 3-letter uppercase ISO-4217 code", retryable: false);
    }
}
