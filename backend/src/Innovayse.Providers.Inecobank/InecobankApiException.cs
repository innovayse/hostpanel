namespace Innovayse.Providers.Inecobank;

/// <summary>
/// Raised when the Inecobank gateway answers with a non-zero errorCode, or when the
/// gateway could not be reached / did not answer with valid JSON at all (see
/// <see cref="InecobankApiClient.TransportErrorCode"/>). Callers rely on this being the
/// only exception type <see cref="InecobankApiClient"/> throws, so every failure surfaced
/// to them — gateway-reported or transport-level — is wrapped in this type.
/// </summary>
public sealed class InecobankApiException : Exception
{
    /// <summary>Initializes the exception with the gateway error code and message.</summary>
    /// <param name="errorCode">The gateway errorCode field.</param>
    /// <param name="message">The gateway errorMessage, or a generic fallback.</param>
    public InecobankApiException(int errorCode, string message)
        : base(message) => ErrorCode = errorCode;

    /// <summary>Initializes the exception wrapping a lower-level transport or parsing failure.</summary>
    /// <param name="errorCode">
    /// <see cref="InecobankApiClient.TransportErrorCode"/> for a failure detected on this
    /// side of the HTTP call (never a code the gateway itself reported).
    /// </param>
    /// <param name="message">A message naming the endpoint and describing the failure.</param>
    /// <param name="innerException">The original transport or parsing exception.</param>
    public InecobankApiException(int errorCode, string message, Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;

    /// <summary>Gets the gateway error code (see the merchant manual's error tables).</summary>
    public int ErrorCode { get; }
}
