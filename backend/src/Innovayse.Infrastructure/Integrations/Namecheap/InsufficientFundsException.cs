namespace Innovayse.Infrastructure.Integrations.Namecheap;

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
