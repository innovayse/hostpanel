namespace Innovayse.Application.Support.Commands.CreatePredefinedReplyCategory;

/// <summary>Command to create a new predefined reply category.</summary>
/// <param name="Name">The category name.</param>
/// <param name="ParentCategoryId">FK to the parent category, or null for a root category.</param>
public record CreatePredefinedReplyCategoryCommand(string Name, int? ParentCategoryId);
