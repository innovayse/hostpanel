namespace Innovayse.Application.Common.Options;

/// <summary>
/// Where the customer-facing client portal is reachable, for the links this backend puts into
/// outgoing mail.
/// </summary>
/// <remarks>
/// Not a section: <c>ClientBaseUrl</c> is a single bare top-level key, so the class names the key
/// it is built from rather than a section it does not have.
/// </remarks>
public sealed class ClientPortalOptions
{
    /// <summary>The configuration key this value is read from. Not a section -- a bare top-level key.</summary>
    public const string ConfigurationKey = "ClientBaseUrl";

    /// <summary>
    /// Absolute base URL of the client portal, scheme included and no trailing slash -- paths such
    /// as <c>/client/accept-invite</c> are appended to it. The default is the Nuxt dev server on a
    /// developer machine, so a deployed tier must override it or its invitation and password-reset
    /// mails point at localhost.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";
}
