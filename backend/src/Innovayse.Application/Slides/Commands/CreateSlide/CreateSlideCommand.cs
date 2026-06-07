namespace Innovayse.Application.Slides.Commands.CreateSlide;

using Innovayse.Application.Slides.DTOs;
using Innovayse.Domain.Slides;

/// <summary>Command to create a new homepage slide with one or more locale translations.</summary>
/// <param name="IconName">MDI icon name (e.g. "cloud-check").</param>
/// <param name="BrandColor">Hex brand color (e.g. "#0ea5e9").</param>
/// <param name="ImageUrl">Image URL or path.</param>
/// <param name="DemoUrl">Optional external demo link.</param>
/// <param name="LearnMoreUrl">Optional learn-more page link.</param>
/// <param name="ProductId">Optional FK to a linked product for pricing data.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the slide should be immediately active.</param>
/// <param name="TargetAudience">Target audience for this slide.</param>
/// <param name="VisibleFrom">Optional start of the visibility window.</param>
/// <param name="VisibleUntil">Optional end of the visibility window.</param>
/// <param name="Translations">Per-locale translations to set on creation.</param>
public record CreateSlideCommand(
    string IconName,
    string BrandColor,
    string ImageUrl,
    string? DemoUrl,
    string? LearnMoreUrl,
    int? ProductId,
    int SortOrder,
    bool IsActive,
    SlideAudience TargetAudience,
    DateTimeOffset? VisibleFrom,
    DateTimeOffset? VisibleUntil,
    List<SlideTranslationDto> Translations);
