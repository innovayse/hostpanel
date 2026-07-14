namespace Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Port for communicating with the mail server (Mailcow).
/// Implemented in Infrastructure — never called directly from the Domain.
/// </summary>
public interface IMailServerClient
{
    /// <summary>Provisions a new domain on the mail server and returns the server-side reference.</summary>
    /// <param name="domain">Fully-qualified domain name to provision.</param>
    /// <param name="maxQuotaMb">Total quota in megabytes to allocate to the domain.</param>
    /// <param name="maxMailboxes">Maximum number of mailboxes permitted on the domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mail server's identifier for the created domain.</returns>
    Task<string> CreateDomainAsync(string domain, int maxQuotaMb, int maxMailboxes, CancellationToken ct);

    /// <summary>Removes a domain and all its mailboxes/aliases from the mail server.</summary>
    /// <param name="domain">Fully-qualified domain name to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteDomainAsync(string domain, CancellationToken ct);

    /// <summary>Generates a DKIM key pair for the domain and returns the public key.</summary>
    /// <param name="domain">Fully-qualified domain name.</param>
    /// <param name="keySize">RSA key size in bits (e.g. 2048).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The DKIM public key, or <see langword="null"/> if generation failed.</returns>
    Task<string?> GenerateDkimAsync(string domain, int keySize, CancellationToken ct);

    /// <summary>Creates a new mailbox on the mail server.</summary>
    /// <param name="email">Full email address (e.g. "john@example.com").</param>
    /// <param name="password">Initial password for the mailbox.</param>
    /// <param name="displayName">Display name for the mailbox owner.</param>
    /// <param name="quotaMb">Storage quota in megabytes.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateMailboxAsync(string email, string password, string displayName, int quotaMb, CancellationToken ct);

    /// <summary>Deletes a mailbox from the mail server.</summary>
    /// <param name="email">Full email address of the mailbox to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteMailboxAsync(string email, CancellationToken ct);

    /// <summary>Updates the password for an existing mailbox.</summary>
    /// <param name="email">Full email address of the mailbox.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateMailboxPasswordAsync(string email, string newPassword, CancellationToken ct);

    /// <summary>Creates an alias that forwards from a source to a destination address.</summary>
    /// <param name="source">Source email address (e.g. "info@example.com").</param>
    /// <param name="destination">Destination email address (e.g. "john@example.com").</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreateAliasAsync(string source, string destination, CancellationToken ct);

    /// <summary>Deletes a mail alias from the mail server.</summary>
    /// <param name="aliasId">The server-side alias identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAliasAsync(int aliasId, CancellationToken ct);
}
