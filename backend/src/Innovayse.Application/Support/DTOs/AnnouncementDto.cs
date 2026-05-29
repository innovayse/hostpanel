namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a single announcement returned by the API.</summary>
/// <param name="Id">The announcement identifier.</param>
/// <param name="Title">The announcement title.</param>
/// <param name="Content">The full announcement body content.</param>
/// <param name="IsPublished">Whether the announcement is visible to clients.</param>
/// <param name="CreatedAt">UTC timestamp when the announcement was created.</param>
public record AnnouncementDto(int Id, string Title, string Content, bool IsPublished, DateTimeOffset CreatedAt);
