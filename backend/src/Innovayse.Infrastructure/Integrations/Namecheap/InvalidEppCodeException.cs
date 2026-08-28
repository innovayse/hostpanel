namespace Innovayse.Infrastructure.Integrations.Namecheap;

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
