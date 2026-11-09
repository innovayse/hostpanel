using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// Tracks a reminder email sent to a client about domain expiry.
/// Created by the notification system; read-only from admin perspective.
/// </summary>
public sealed class DomainReminder : BaseEntity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the kind of reminder that was sent.</summary>
    public DomainReminderType ReminderType { get; private set; }

    /// <summary>Gets the recipient email address.</summary>
    public string SentTo { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the reminder was sent.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private DomainReminder() { }

    /// <summary>
    /// Creates a new domain reminder record.
    /// </summary>
    /// <param name="domainId">FK to the owning domain.</param>
    /// <param name="reminderType">Kind of reminder that was sent.</param>
    /// <param name="sentTo">Recipient email address.</param>
    /// <param name="sentAt">When the reminder was sent, UTC.</param>
    internal DomainReminder(int domainId, DomainReminderType reminderType, string sentTo, DateTimeOffset sentAt)
    {
        DomainId = domainId;
        ReminderType = reminderType;
        SentTo = sentTo;
        SentAt = sentAt;
    }
}
