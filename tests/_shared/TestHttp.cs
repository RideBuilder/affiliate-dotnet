using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RideBuilder.Affiliate.TestSupport;

/// <summary>A request the <see cref="RecordingHandler"/> captured.</summary>
internal sealed record RecordedRequest(string Method, string Path, string? Authorization, string? Body);

/// <summary>
/// Test double for the transport — the C# analog of the Node tests' recording <c>fetch</c>. Records each
/// request and returns canned responses in order (repeating the last once the queue drains).
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<(int Status, string? Body)> _responses;
    private readonly (int Status, string? Body) _last;

    public List<RecordedRequest> Requests { get; } = new();

    public RecordingHandler(params (int Status, string? Body)[] responses)
    {
        _responses = new Queue<(int, string?)>(responses);
        _last = responses.Length > 0 ? responses[responses.Length - 1] : (200, null);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
        var auth = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(",", values) : null;
        Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.AbsolutePath, auth, body));

        var (status, respBody) = _responses.Count > 0 ? _responses.Dequeue() : _last;
        var message = new HttpResponseMessage((HttpStatusCode)status);
        if (respBody is not null)
            message.Content = new StringContent(respBody, Encoding.UTF8, "application/json");
        return message;
    }
}

/// <summary>Builds a <see cref="RideBuilderClient"/> wired to a <see cref="RecordingHandler"/> with no real delays.</summary>
internal static class TestClient
{
    /// <summary>A no-op sleeper so retry logic runs instantly in tests.</summary>
    public static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

    public static RideBuilderClient Create(RideBuilderClientOptions options, RecordingHandler handler)
        => new(options, new HttpClient(handler), NoDelay);
}
