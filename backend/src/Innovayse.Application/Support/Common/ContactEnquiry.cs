namespace Innovayse.Application.Support.Common;

/// <summary>
/// One submission of the public website's contact form, after the handler has trimmed it and
/// checked its lengths -- the shape a notification channel renders.
/// </summary>
/// <remarks>
/// <para>
/// A type of its own rather than handing <c>SendContactMessageCommand</c> to the channel. The
/// command is the wire shape as the visitor typed it, with every field still unchecked; this is
/// what survived validation, so a channel cannot render a value the mail would have refused. It
/// also keeps the port free of the command type, which belongs to one use case's folder.
/// </para>
/// <para>
/// In <c>Support/Common/</c> rather than beside the command because it is the vocabulary
/// <see cref="Innovayse.Application.Support.Interfaces.IContactNotifier"/> is written in, and a
/// port's contract has to be readable without opening the use case that happens to call it
/// today.
/// </para>
/// </remarks>
/// <param name="Name">The sender's name, trimmed. Never empty.</param>
/// <param name="Email">The sender's email address, trimmed. Never empty; contains an <c>@</c>.</param>
/// <param name="Phone">The sender's phone number, or <see langword="null"/> when they gave none.</param>
/// <param name="Service">The service the enquiry is about, or <see langword="null"/> when unpicked.</param>
/// <param name="Message">The message body, trimmed. Never empty.</param>
/// <param name="SubmittedAt">
/// The send time the browser formatted for its own locale. Free text, not a timestamp: it is the
/// visitor's local wall clock. The handler substitutes its own UTC time when the browser sent
/// none, so a channel never has to invent one.
/// </param>
public record ContactEnquiry(
    string Name,
    string Email,
    string? Phone,
    string? Service,
    string Message,
    string SubmittedAt);
