namespace Innovayse.Application.Notifications.DTOs;

/// <summary>DTO representing a single email log entry returned by queries.</summary>
/// <param name="Id">The unique identifier of the log entry.</param>
/// <param name="To">The recipient email address.</param>
/// <param name="Subject">The email subject line that was sent.</param>
/// <param name="SentAt">The UTC timestamp at which the send was attempted.</param>
/// <param name="Success">Whether the email was delivered successfully.</param>
/// <param name="Error">Error message if the send failed; <see langword="null"/> on success.</param>
public record EmailLogDto(int Id, string To, string Subject, DateTimeOffset SentAt, bool Success, string? Error);
