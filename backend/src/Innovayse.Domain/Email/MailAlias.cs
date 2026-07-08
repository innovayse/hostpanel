namespace Innovayse.Domain.Email;

using Innovayse.Domain.Common;

/// <summary>
/// Represents an email alias that forwards from a source address to a destination address.
/// Owned by the <see cref="EmailDomain"/> aggregate — cannot exist independently.
/// </summary>
public sealed class MailAlias : Entity
{
    /// <summary>Gets the FK to the owning <see cref="EmailDomain"/>.</summary>
    public int EmailDomainId { get; private set; }

    /// <summary>Gets the source email address (e.g. "info@example.com").</summary>
    public string SourceAddress { get; private set; } = null!;

    /// <summary>Gets the destination email address (e.g. "john@example.com").</summary>
    public string DestinationAddress { get; private set; } = null!;

    /// <summary>Gets whether the alias is currently active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC timestamp when the alias was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private MailAlias() : base(0) { }

    /// <summary>
    /// Creates a new mail alias owned by the specified email domain.
    /// </summary>
    /// <param name="emailDomainId">FK to the owning <see cref="EmailDomain"/>.</param>
    /// <param name="source">Source email address.</param>
    /// <param name="destination">Destination email address.</param>
    /// <returns>A new active <see cref="MailAlias"/>.</returns>
    internal static MailAlias Create(int emailDomainId, string source, string destination)
    {
        return new MailAlias
        {
            EmailDomainId = emailDomainId,
            SourceAddress = source.ToLowerInvariant().Trim(),
            DestinationAddress = destination.ToLowerInvariant().Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
