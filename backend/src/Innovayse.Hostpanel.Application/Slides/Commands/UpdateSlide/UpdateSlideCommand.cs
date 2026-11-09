using Innovayse.Hostpanel.Application.Slides.Common;
using Innovayse.Hostpanel.Domain.Slides.Enums;

namespace Innovayse.Hostpanel.Application.Slides.Commands.UpdateSlide;

/// <summary>Command to update an existing homepage slide and replace its translations.</summary>
/// <param name="Id">
/// The slide identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> rather than being treated as
/// a required member, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
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
public sealed record UpdateSlideCommand(
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
