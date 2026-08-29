namespace Innovayse.API.Support.Requests;

/// <summary>Request body for a submission of the public website's contact form.</summary>
/// <remarks>
/// Only <see cref="Name"/>, <see cref="Email"/> and <see cref="Message"/> are marked
/// <c>required</c>, and that is a binding concern rather than the validation: the handler trims
/// and length-checks every field, and a caller that omits an optional one must not get a
/// model-binding error page instead of the API's own JSON refusal.
/// </remarks>
public sealed class SendContactMessageRequest
{
    /// <summary>Gets the sender's name, as typed.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the sender's email address, as typed. Where a reply would go.</summary>
    public required string Email { get; init; }

    /// <summary>Gets the sender's phone number, or <see langword="null"/> when they gave none.</summary>
    public string? Phone { get; init; }

    /// <summary>Gets the service the enquiry is about, or <see langword="null"/> when unpicked.</summary>
    public string? Service { get; init; }

    /// <summary>Gets the message body, as typed.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the send time the browser formatted for its own locale, or <see langword="null"/>.
    /// Free text, not a timestamp: it is the visitor's local wall clock and is only read in the
    /// resulting mail.
    /// </summary>
    public string? SubmittedAt { get; init; }
}
