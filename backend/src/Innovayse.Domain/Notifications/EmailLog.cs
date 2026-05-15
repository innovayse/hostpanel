namespace Innovayse.Domain.Notifications;

using Innovayse.Domain.Common;

/// <summary>
/// Records the outcome of an email send attempt.
/// Used for audit trailing and delivery diagnostics.
/// </summary>
public sealed class EmailLog : Entity
{
    /// <summary>Gets the recipient email address.</summary>
    public string To { get; private set; } = string.Empty;

    /// <summary>Gets the email subject that was sent.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the rendered email body that was sent.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp at which the send was attempted.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Gets a value indicating whether the email was sent successfully.</summary>
    public bool Success { get; private set; }

    /// <summary>Gets the error message if the send failed; <see langword="null"/> on success.</summary>
    public string? Error { get; private set; }

    /// <summary>Initialises a new <see cref="EmailLog"/> for EF Core materialisation.</summary>
    internal EmailLog() : base(0)
    {
    }

    /// <summary>Initialises an <see cref="EmailLog"/> with the given identity.</summary>
    /// <param name="id">The entity identifier.</param>
    private EmailLog(int id) : base(id)
    {
    }

    /// <summary>
    /// Creates a new email log entry recording the result of a send attempt.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="body">The rendered email body.</param>
    /// <param name="success">Whether the email was sent successfully.</param>
    /// <param name="error">Error message if the send failed; <see langword="null"/> on success.</param>
    /// <returns>A new <see cref="EmailLog"/> with <see cref="SentAt"/> set to the current UTC time.</returns>
    public static EmailLog Create(string to, string subject, string body, bool success, string? error)
    {
        return new EmailLog(0)
        {
            To = to,
            Subject = subject,
            Body = body,
            SentAt = DateTimeOffset.UtcNow,
            Success = success,
            Error = error,
        };
    }
}
