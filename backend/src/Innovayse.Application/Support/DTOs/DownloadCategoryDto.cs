namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a download category with its download count.</summary>
/// <param name="Id">Category primary key.</param>
/// <param name="Name">Category display name.</param>
/// <param name="Description">Category description text.</param>
/// <param name="IsHidden">Whether the category is hidden from clients.</param>
/// <param name="ParentCategoryId">FK to parent category, or null if top-level.</param>
/// <param name="DownloadCount">Number of downloads in this category.</param>
public record DownloadCategoryDto(
    int Id,
    string Name,
    string Description,
    bool IsHidden,
    int? ParentCategoryId,
    int DownloadCount);
