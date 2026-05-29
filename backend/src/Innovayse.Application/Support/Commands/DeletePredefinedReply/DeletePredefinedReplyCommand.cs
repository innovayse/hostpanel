namespace Innovayse.Application.Support.Commands.DeletePredefinedReply;

/// <summary>Command to permanently delete a predefined reply.</summary>
/// <param name="Id">The reply identifier.</param>
public record DeletePredefinedReplyCommand(int Id);
