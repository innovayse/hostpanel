namespace Innovayse.Application.Support.Commands.CreatePredefinedReply;

/// <summary>Command to create a new predefined reply.</summary>
/// <param name="Name">The reply name / label.</param>
/// <param name="Content">The reply content text.</param>
/// <param name="CategoryId">FK to the category.</param>
public record CreatePredefinedReplyCommand(string Name, string Content, int CategoryId);
