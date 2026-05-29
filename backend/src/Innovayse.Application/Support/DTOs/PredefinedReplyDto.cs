namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a predefined reply.</summary>
/// <param name="Id">Reply primary key.</param>
/// <param name="Name">The reply name / label.</param>
/// <param name="Content">The reply content text.</param>
/// <param name="CategoryId">FK to the category.</param>
/// <param name="CategoryName">Name of the category, if resolved.</param>
public record PredefinedReplyDto(int Id, string Name, string Content, int CategoryId, string? CategoryName);
