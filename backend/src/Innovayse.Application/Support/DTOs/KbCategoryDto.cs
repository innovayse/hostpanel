namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a knowledge base category.</summary>
/// <param name="Id">Category primary key.</param>
/// <param name="Name">Category display name.</param>
/// <param name="Description">Category description text.</param>
/// <param name="IsHidden">Whether the category is hidden from clients.</param>
/// <param name="ParentCategoryId">FK to parent category, or null if top-level.</param>
/// <param name="ArticleCount">Number of articles in this category.</param>
public record KbCategoryDto(
    int Id,
    string Name,
    string Description,
    bool IsHidden,
    int? ParentCategoryId,
    int ArticleCount);
