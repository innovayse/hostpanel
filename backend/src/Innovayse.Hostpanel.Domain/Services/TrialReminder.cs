using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// Records that the reminder for one reminder day of a free trial was sent, so it is sent once.
/// Stored in the <c>TrialReminders</c> table.
/// </summary>
public sealed class TrialReminder : BaseEntity
{
    /// <summary>Gets the FK to the trial service.</summary>
    public int ServiceId { get; private set; }

    /// <summary>Gets the reminder day: how many days before the trial end the reminder is for.</summary>
    public int DaysBefore { get; private set; }

    /// <summary>Gets the UTC moment the reminder was recorded.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private TrialReminder() { }

    /// <summary>Records a reminder and raises <see cref="TrialEndingSoonEvent"/>.</summary>
    /// <param name="service">The trial service the reminder is for.</param>
    /// <param name="daysBefore">The reminder day.</param>
    /// <param name="now">The current moment (UTC), read by the caller.</param>
    /// <returns>The new <see cref="TrialReminder"/>, not yet saved.</returns>
    public static TrialReminder Create(ClientService service, int daysBefore, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(service);

        var reminder = new TrialReminder { ServiceId = service.Id, DaysBefore = daysBefore, SentAt = now };
        reminder.AddDomainEvent(new TrialEndingSoonEvent(
            service.Id, service.ClientId, daysBefore, service.TrialEndsAt ?? now, now));

        return reminder;
    }
}
