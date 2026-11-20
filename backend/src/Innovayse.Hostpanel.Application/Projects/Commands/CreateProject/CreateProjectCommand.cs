using Innovayse.Hostpanel.Application.Projects.Common;

namespace Innovayse.Hostpanel.Application.Projects.Commands.CreateProject;

/// <summary>Command to create a project with one or more locale translations.</summary>
/// <param name="Slug">The project's URL-safe identifier.</param>
/// <param name="Url">The live project's website, or <c>null</c></param>
/// <param name="ImageUrls">The project's image URLs, in display order.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the project is shown on the storefront.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record CreateProjectCommand(
    string Slug,
    string? Url,
    string[] ImageUrls,
    int SortOrder,
    bool IsActive,
    Dictionary<string, ProjectTranslationDto> Translations);
