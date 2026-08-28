namespace Innovayse.Infrastructure.Integrations.Namecheap;

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
