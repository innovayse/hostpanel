namespace Innovayse.Application.Notifications.DTOs;

/// <summary>One email as it was sent, body included.</summary>
/// <param name="Id">The log entry's primary key.</param>
/// <param name="To">The address it was sent to.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The rendered body.</param>
/// <param name="SentAt">When it was sent.</param>
/// <param name="Success">Whether delivery to the mail server succeeded.</param>
/// <param name="Error">Why it failed, when it did.</param>
/// <remarks>
/// Separate from <see cref="EmailLogDto"/> because of the body: a list of a client's mail is a
/// list of subjects, and shipping every rendered message with it would send far more than the
/// screen shows.
/// </remarks>
public record EmailLogDetailDto(
    int Id, string To, string Subject, string Body,
    DateTimeOffset SentAt, bool Success, string? Error);
