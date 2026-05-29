namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a predefined reply category with its reply count.</summary>
/// <param name="Id">Category primary key.</param>
/// <param name="Name">The category name.</param>
/// <param name="ParentCategoryId">FK to the parent category, or null if root.</param>
/// <param name="ReplyCount">Number of replies in this category.</param>
public record PredefinedReplyCategoryDto(int Id, string Name, int? ParentCategoryId, int ReplyCount);
