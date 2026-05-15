namespace Innovayse.API.Support.Requests;

/// <summary>Request body for adding a reply to a support ticket.</summary>
public sealed class ReplyToTicketRequest
{
    /// <summary>Gets or sets the reply message body.</summary>
    public required string Message { get; init; }

    /// <summary>Gets or sets the display name of the reply author.</summary>
    public required string AuthorName { get; init; }
}
