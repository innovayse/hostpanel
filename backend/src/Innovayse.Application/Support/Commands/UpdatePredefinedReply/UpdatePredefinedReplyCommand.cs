namespace Innovayse.Application.Support.Commands.UpdatePredefinedReply;

/// <summary>Command to update an existing predefined reply.</summary>
/// <param name="Id">The reply identifier.</param>
/// <param name="Name">The new reply name / label.</param>
/// <param name="Content">The new reply content text.</param>
/// <param name="CategoryId">The new category FK.</param>
public record UpdatePredefinedReplyCommand(int Id, string Name, string Content, int CategoryId);
