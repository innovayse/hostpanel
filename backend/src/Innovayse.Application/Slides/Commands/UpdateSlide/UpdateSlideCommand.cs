namespace Innovayse.Application.Slides.Commands.UpdateSlide;

using Innovayse.Application.Slides.DTOs;
using Innovayse.Domain.Slides;

/// <summary>Command to update an existing homepage slide and replace its translations.</summary>
/// <param name="Id">The identifier of the slide to update.</param>
/// <param name="IconName">New MDI icon name.</param>
/// <param name="BrandColor">New hex brand color.</param>
/// <param name="ImageUrl">New image URL or path.</param>
/// <param name="DemoUrl">New optional external demo link.</param>
/// <param name="LearnMoreUrl">New optional learn-more page link.</param>
/// <param name="ProductId">New optional FK to a linked product.</param>
/// <param name="SortOrder">New display order.</param>
/// <param name="IsActive">New active state.</param>
/// <param name="TargetAudience">New target audience.</param>
/// <param name="VisibleFrom">New optional start of the visibility window.</param>
/// <param name="VisibleUntil">New optional end of the visibility window.</param>
/// <param name="Translations">Replacement set of per-locale translations.</param>
public record UpdateSlideCommand(
    int Id,
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
