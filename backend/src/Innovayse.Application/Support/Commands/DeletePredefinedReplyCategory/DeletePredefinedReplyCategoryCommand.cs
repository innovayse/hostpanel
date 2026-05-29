namespace Innovayse.Application.Support.Commands.DeletePredefinedReplyCategory;

/// <summary>Command to permanently delete a predefined reply category.</summary>
/// <param name="Id">The category identifier.</param>
public record DeletePredefinedReplyCategoryCommand(int Id);
