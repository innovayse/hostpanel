namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a single reply on a support ticket.</summary>
/// <param name="Id">Reply primary key.</param>
/// <param name="Message">The reply body text.</param>
/// <param name="AuthorName">Display name of the reply author.</param>
/// <param name="IsStaffReply">Whether the reply was posted by a staff member.</param>
/// <param name="CreatedAt">UTC timestamp when the reply was created.</param>
public record TicketReplyDto(
    int Id,
    string Message,
    string AuthorName,
    bool IsStaffReply,
    DateTimeOffset CreatedAt);
