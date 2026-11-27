using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// One extra value a registry asks for beside the standard contact, as the registrar plugin's manifest declares it, with every
/// text already in the caller's language: the screens render the field and never translate or interpret it.
/// </summary>
/// <param name="Key">The key the value is sent and stored under.</param>
/// <param name="Label">The text shown for the field.</param>
/// <param name="Type">The input kind.</param>
/// <param name="Required">Whether the field must have a value before the operation goes ahead.</param>
/// <param name="Description">The help text shown under the field, or <see langword="null"/> for none.</param>
/// <param name="Placeholder">The hint shown in an empty input, or <see langword="null"/> for none.</param>
/// <param name="Pattern">The regular expression the whole value must match, or <see langword="null"/> for none.</param>
/// <param name="MinLength">The fewest characters of a value, or <see langword="null"/> for no lower bound.</param>
/// <param name="MaxLength">The most characters of a value, or <see langword="null"/> for no upper bound.</param>
/// <param name="Options">The choices of a select field; empty for every other kind.</param>
public sealed record DomainFieldDto(
    string Key,
    string Label,
    PluginDomainFieldType Type,
    bool Required,
    string? Description,
    string? Placeholder,
    string? Pattern,
    int? MinLength,
    int? MaxLength,
    IReadOnlyList<DomainFieldOptionDto> Options);
