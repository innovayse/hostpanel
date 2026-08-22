namespace Innovayse.Providers.Inecobank;

/// <summary>Raised when the Inecobank gateway answers with a non-zero errorCode.</summary>
public sealed class InecobankApiException : Exception
{
    /// <summary>Initializes the exception with the gateway error code and message.</summary>
    /// <param name="errorCode">The gateway errorCode field.</param>
    /// <param name="message">The gateway errorMessage, or a generic fallback.</param>
    public InecobankApiException(int errorCode, string message)
        : base(message) => ErrorCode = errorCode;

    /// <summary>Gets the gateway error code (see the merchant manual's error tables).</summary>
    public int ErrorCode { get; }
}
