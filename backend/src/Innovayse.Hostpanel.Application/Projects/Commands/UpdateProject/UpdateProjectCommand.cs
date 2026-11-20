using Innovayse.Hostpanel.Application.Projects.Common;

namespace Innovayse.Hostpanel.Application.Projects.Commands.UpdateProject;

/// <summary>Command to update an existing project and replace its translations.</summary>
/// <param name="Id">
/// The project identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Slug">The project's URL-safe identifier.</param>
/// <param name="Url">The live project's website, or <c>null</c></param>
/// <param name="ImageUrls">The project's image URLs, in display order.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the project is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record UpdateProjectCommand(
    int Id,
    string Slug,
    string? Url,
    string[] ImageUrls,
    int SortOrder,
    bool IsActive,
    Dictionary<string, ProjectTranslationDto> Translations);
