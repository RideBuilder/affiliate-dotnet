using System;

namespace RideBuilder.Affiliate;

/// <summary>
/// Error raised by the SDK. Port of the Node <c>RideBuilderError</c>. <see cref="Retryable"/> distinguishes
/// a transient failure (5xx/429/network/timeout, retried internally) from a terminal one (invalid input,
/// or a non-retryable 4xx such as 401/413/400).
/// </summary>
public sealed class RideBuilderException : Exception
{
    /// <summary>HTTP status code, when the error came from a response.</summary>
    public int? Status { get; }

    /// <summary>Server error code from the <c>{"error": "..."}</c> body (e.g. <c>"invalid_auth"</c>), when present.</summary>
    public string? ErrorCode { get; }

    /// <summary>True for transient failures the SDK retries; false for terminal failures.</summary>
    public bool Retryable { get; }

    /// <summary>Create a <see cref="RideBuilderException"/>.</summary>
    public RideBuilderException(string message, bool retryable, int? status = null, string? errorCode = null)
        : base(message)
    {
        Retryable = retryable;
        Status = status;
        ErrorCode = errorCode;
    }
}
