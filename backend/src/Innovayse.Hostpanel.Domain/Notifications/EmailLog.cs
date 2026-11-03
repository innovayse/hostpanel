using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Notifications;

/// <summary>
/// Records the outcome of an email send attempt.
/// Used for audit trailing and delivery diagnostics.
/// </summary>
public sealed class EmailLog : BaseEntity
{
    /// <summary>Gets the recipient email address.</summary>
    public string To { get; private set; } = string.Empty;

    /// <summary>Gets the email subject that was sent.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the rendered email body that was sent.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the UTC moment of the latest attempt: the claim time while <see cref="State"/> is
    /// <see cref="EmailDeliveryState.Sending"/>, the outcome time once the relay answered.
    /// </summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Gets a value indicating whether the email was sent successfully; always equal to <see cref="State"/> being <see cref="EmailDeliveryState.Sent"/>.</summary>
    public bool Success { get; private set; }

    /// <summary>Gets why the send failed; <see langword="null"/> on success and while sending.</summary>
    public EmailSendFailure? Failure { get; private set; }

    /// <summary>
    /// Gets the delivery key: 64 lower-case hex characters (SHA-256) naming one message to one address, so a
    /// redelivered event cannot mail twice; <see langword="null"/> for an unkeyed send. Unique where set.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Gets where this delivery stands.</summary>
    public EmailDeliveryState State { get; private set; }

    /// <summary>
    /// Whether a delivery under this row's key may be attempted again: after a failure, or when a claim
    /// was left in <see cref="EmailDeliveryState.Sending"/> past its lease (the process died between the
    /// claim and the outcome). A delivered row never may.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="claimLease">How long a claim is trusted to be in flight.</param>
    /// <returns><see langword="true"/> when the caller may <see cref="Reclaim"/> the row and send.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The row holds a state this version does not define.</exception>
    public bool MayResend(DateTimeOffset now, TimeSpan claimLease)
    {
        return State switch
        {
            EmailDeliveryState.Failed => true,
            EmailDeliveryState.Sending => SentAt + claimLease <= now,
            EmailDeliveryState.Sent => false,
            _ => throw new ArgumentOutOfRangeException(nameof(State), State, "Unknown delivery state."),
        };
    }

    /// <summary>Initialises a new <see cref="EmailLog"/> for EF Core materialisation.</summary>
    internal EmailLog()
    {
    }

    /// <summary>Initialises an <see cref="EmailLog"/> with the given identity.</summary>
    /// <param name="id">The entity identifier.</param>
    private EmailLog(int id)
    {
        Id = id;
    }

    /// <summary>Moves the send time to the moment the message was really sent, for log lines seeded from history.</summary>
    /// <param name="sentAt">The moment the message was sent (UTC).</param>
    public void Backdate(DateTimeOffset sentAt) => SentAt = sentAt;

    /// <summary>
    /// Creates a new email log entry recording the result of a send attempt.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="body">The rendered email body.</param>
    /// <param name="failure">Why the send failed; <see langword="null"/> when the relay accepted it.</param>
    /// <param name="sentAt">When the send was attempted, UTC.</param>
    /// <returns>A new <see cref="EmailLog"/> stamped with <paramref name="sentAt"/>, successful exactly when <paramref name="failure"/> is null.</returns>
    public static EmailLog Create(string to, string subject, string body, EmailSendFailure? failure, DateTimeOffset sentAt)
    {
        return new EmailLog(0)
        {
            To = to,
            Subject = subject,
            Body = body,
            SentAt = sentAt,
            Success = failure is null,
            Failure = failure,
            State = failure is null ? EmailDeliveryState.Sent : EmailDeliveryState.Failed,
        };
    }

    /// <summary>
    /// Claims a keyed delivery before the relay is asked, so a second delivery of the same message finds
    /// the row and does not send.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="body">The rendered email body.</param>
    /// <param name="idempotencyKey">The delivery key, 64 lower-case hex characters.</param>
    /// <param name="now">The claim moment (UTC).</param>
    /// <returns>A new row in <see cref="EmailDeliveryState.Sending"/>, stamped with <paramref name="now"/>.</returns>
    public static EmailLog Claim(string to, string subject, string body, string idempotencyKey, DateTimeOffset now)
    {
        return new EmailLog(0)
        {
            To = to,
            Subject = subject,
            Body = body,
            SentAt = now,
            Success = false,
            Failure = null,
            IdempotencyKey = idempotencyKey,
            State = EmailDeliveryState.Sending,
        };
    }

    /// <summary>Records that the relay accepted the message.</summary>
    /// <param name="now">The moment the relay answered (UTC).</param>
    public void MarkDelivered(DateTimeOffset now)
    {
        State = EmailDeliveryState.Sent;
        Success = true;
        Failure = null;
        SentAt = now;
    }

    /// <summary>Records that the relay did not accept the message; a later delivery may try again.</summary>
    /// <param name="failure">Why the relay did not accept it.</param>
    /// <param name="now">The moment the attempt ended (UTC).</param>
    public void MarkUndelivered(EmailSendFailure failure, DateTimeOffset now)
    {
        State = EmailDeliveryState.Failed;
        Success = false;
        Failure = failure;
        SentAt = now;
    }

    /// <summary>
    /// Claims the row again for a new attempt. The caller asks <see cref="MayResend"/> first; the row's
    /// concurrency token makes sure only one of two concurrent reclaims is saved.
    /// </summary>
    /// <param name="now">The claim moment (UTC).</param>
    /// <exception cref="DomainRuleException">The message was already delivered.</exception>
    public void Reclaim(DateTimeOffset now)
    {
        if (State == EmailDeliveryState.Sent)
        {
            throw new DomainRuleException(
                NotificationsErrors.EmailAlreadyDelivered, "A delivered email cannot be claimed again.");
        }

        State = EmailDeliveryState.Sending;
        Success = false;
        Failure = null;
        SentAt = now;
    }
}
