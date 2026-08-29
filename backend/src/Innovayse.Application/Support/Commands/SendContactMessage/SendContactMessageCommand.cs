namespace Innovayse.Application.Support.Commands.SendContactMessage;

/// <summary>
/// Command carrying one submission of the public website's contact form to the operator's
/// enquiry inbox.
/// </summary>
/// <remarks>
/// Unauthenticated: this is the form a visitor who has no account fills in, so nothing here is
/// resolved from a credential. <see cref="Email"/> is the visitor's own claim about how to reach
/// them and is not verified -- it is rendered into the mail as text and used as the
/// <c>Reply-To</c> hint in the body, never as an identity.
/// </remarks>
/// <param name="Name">The sender's name, as typed.</param>
/// <param name="Email">The sender's email address, as typed. Where a reply would go.</param>
/// <param name="Phone">The sender's phone number, when they gave one; otherwise <see langword="null"/>.</param>
/// <param name="Service">Which service the enquiry is about, when they picked one; otherwise <see langword="null"/>.</param>
/// <param name="Message">The message body, as typed.</param>
/// <param name="SubmittedAt">
/// The send time the browser formatted for its own locale, when it sent one. Kept as free text
/// rather than a <see cref="DateTimeOffset"/> because it is the visitor's local wall clock and
/// exists only to be read in the mail; the handler stamps its own UTC time when it is absent.
/// </param>
public record SendContactMessageCommand(
    string Name,
    string Email,
    string? Phone,
    string? Service,
    string Message,
    string? SubmittedAt);
