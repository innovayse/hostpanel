namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Tracks a reminder email sent to a client about domain expiry.
/// Created by the notification system; read-only from admin perspective.
/// </summary>
public sealed class DomainReminder : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the type of reminder (e.g. "30 Days Before Expiry").</summary>
    public string ReminderType { get; private set; } = string.Empty;

    /// <summary>Gets the recipient email address.</summary>
    public string SentTo { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the reminder was sent.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private DomainReminder() : base(0) { }

    /// <summary>
    /// Creates a new domain reminder record.
    /// </summary>
    /// <param name="domainId">FK to the owning domain.</param>
    /// <param name="reminderType">Type label for this reminder.</param>
    /// <param name="sentTo">Recipient email address.</param>
    internal DomainReminder(int domainId, string reminderType, string sentTo) : base(0)
    {
        DomainId = domainId;
        ReminderType = reminderType;
        SentTo = sentTo;
        SentAt = DateTimeOffset.UtcNow;
    }
}
