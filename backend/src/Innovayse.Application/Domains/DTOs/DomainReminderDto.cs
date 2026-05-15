namespace Innovayse.Application.Domains.DTOs;

/// <summary>DTO for a domain expiry reminder record.</summary>
/// <param name="Id">Reminder primary key.</param>
/// <param name="ReminderType">Type of reminder sent.</param>
/// <param name="SentTo">Recipient email address.</param>
/// <param name="SentAt">UTC timestamp when the reminder was sent.</param>
public record DomainReminderDto(int Id, string ReminderType, string SentTo, DateTimeOffset SentAt);
