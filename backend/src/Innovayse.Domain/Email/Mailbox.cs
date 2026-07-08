namespace Innovayse.Domain.Email;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a mailbox (email account) belonging to an <see cref="EmailDomain"/>.
/// Owned by the <see cref="EmailDomain"/> aggregate — cannot exist independently.
/// </summary>
public sealed class Mailbox : Entity
{
    /// <summary>Gets the FK to the owning <see cref="EmailDomain"/>.</summary>
    public int EmailDomainId { get; private set; }

    /// <summary>Gets the local part of the email address (e.g. "john" for john@example.com).</summary>
    public string LocalPart { get; private set; } = null!;

    /// <summary>Gets the display name shown in email clients (e.g. "John Smith").</summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>Gets the mailbox storage quota in megabytes.</summary>
    public int QuotaMb { get; private set; }

    /// <summary>Gets whether the mailbox is currently active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC timestamp when the mailbox was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Mailbox() : base(0) { }

    /// <summary>
    /// Creates a new mailbox owned by the specified email domain.
    /// </summary>
    /// <param name="emailDomainId">FK to the owning <see cref="EmailDomain"/>.</param>
    /// <param name="localPart">Local part of the email address.</param>
    /// <param name="displayName">Display name for the mailbox owner.</param>
    /// <param name="quotaMb">Storage quota in megabytes.</param>
    /// <returns>A new active <see cref="Mailbox"/>.</returns>
    internal static Mailbox Create(int emailDomainId, string localPart, string displayName, int quotaMb)
    {
        return new Mailbox
        {
            EmailDomainId = emailDomainId,
            LocalPart = localPart.ToLowerInvariant().Trim(),
            DisplayName = displayName.Trim(),
            QuotaMb = quotaMb,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Returns the full email address for this mailbox in the given domain.
    /// </summary>
    /// <param name="domainName">The domain name (e.g. "example.com").</param>
    /// <returns>Full email address (e.g. "john@example.com").</returns>
    public string Email(string domainName) => $"{LocalPart}@{domainName}";
}
