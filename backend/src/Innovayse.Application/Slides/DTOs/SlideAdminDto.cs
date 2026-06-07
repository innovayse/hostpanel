namespace Innovayse.Application.Slides.DTOs;

using Innovayse.Domain.Slides;

/// <summary>DTO representing a slide for the admin panel, including all fields and all translations.</summary>
/// <param name="Id">Slide identifier.</param>
/// <param name="IconName">MDI icon name (e.g. "cloud-check").</param>
/// <param name="BrandColor">Hex brand color (e.g. "#0ea5e9").</param>
/// <param name="ImageUrl">Image URL or path.</param>
/// <param name="DemoUrl">Optional external demo link.</param>
/// <param name="LearnMoreUrl">Optional learn-more page link.</param>
/// <param name="ProductId">Optional FK to a linked product.</param>
/// <param name="ProductName">Resolved product name for the linked product, or null.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the slide is currently active.</param>
/// <param name="TargetAudience">Target audience for this slide.</param>
/// <param name="VisibleFrom">Optional start of the visibility window.</param>
/// <param name="VisibleUntil">Optional end of the visibility window.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="Translations">All per-locale translations for this slide.</param>
public record SlideAdminDto(
    int Id,
    string IconName,
    string BrandColor,
    string ImageUrl,
    string? DemoUrl,
    string? LearnMoreUrl,
    int? ProductId,
    string? ProductName,
    int SortOrder,
    bool IsActive,
    SlideAudience TargetAudience,
    DateTimeOffset? VisibleFrom,
    DateTimeOffset? VisibleUntil,
    DateTimeOffset CreatedAt,
    List<SlideTranslationDto> Translations);
