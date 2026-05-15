namespace Innovayse.Infrastructure.Domains.Namecheap;

/// <summary>
/// Exception thrown when a Namecheap registrar API call returns an error response.
/// </summary>
public class RegistrarException : Exception
{
    /// <summary>Gets the Namecheap API error code returned in the response XML.</summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RegistrarException"/> with an error code and message.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code (numeric string).</param>
    /// <param name="message">Human-readable error description.</param>
    public RegistrarException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RegistrarException"/> wrapping an inner exception.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code (numeric string).</param>
    /// <param name="message">Human-readable error description.</param>
    /// <param name="innerException">The underlying exception that caused this error.</param>
    public RegistrarException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates the appropriate <see cref="RegistrarException"/> subclass for a known Namecheap error code,
    /// or returns a base <see cref="RegistrarException"/> for unknown codes.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code string.</param>
    /// <param name="message">Raw error message from the API response.</param>
    /// <returns>A typed registrar exception.</returns>
    internal static RegistrarException FromCode(string errorCode, string message)
    {
        return errorCode switch
        {
            "2019166" => new InsufficientFundsException(errorCode, "Insufficient funds"),
            "2030280" => new DomainNotAvailableException(errorCode, "Domain not available"),
            "2011170" => new InvalidEppCodeException(errorCode, "Invalid EPP code"),
            _ => new RegistrarException(errorCode, message),
        };
    }
}

/// <summary>
/// Thrown when the Namecheap account has insufficient funds to complete the operation (error 2019166).
/// </summary>
public sealed class InsufficientFundsException : RegistrarException
{
    /// <summary>
    /// Initializes a new instance of <see cref="InsufficientFundsException"/>.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code.</param>
    /// <param name="message">Error message.</param>
    public InsufficientFundsException(string errorCode, string message)
        : base(errorCode, message) { }
}

/// <summary>
/// Thrown when the requested domain is not available for registration (error 2030280).
/// </summary>
public sealed class DomainNotAvailableException : RegistrarException
{
    /// <summary>
    /// Initializes a new instance of <see cref="DomainNotAvailableException"/>.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code.</param>
    /// <param name="message">Error message.</param>
    public DomainNotAvailableException(string errorCode, string message)
        : base(errorCode, message) { }
}

/// <summary>
/// Thrown when an invalid EPP authorization code is supplied for a transfer (error 2011170).
/// </summary>
public sealed class InvalidEppCodeException : RegistrarException
{
    /// <summary>
    /// Initializes a new instance of <see cref="InvalidEppCodeException"/>.
    /// </summary>
    /// <param name="errorCode">Namecheap API error code.</param>
    /// <param name="message">Error message.</param>
    public InvalidEppCodeException(string errorCode, string message)
        : base(errorCode, message) { }
}
