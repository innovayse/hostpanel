using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Domains.Extensions;

/// <summary>Maps a registrar plugin's domain field to the DTO the screens render.</summary>
public static class PluginDomainFieldExtensions
{
    /// <summary>Maps a field, its texts resolved in the request's language and falling back to English.</summary>
    /// <param name="field">The field from the manifest's <c>domainFields</c>.</param>
    /// <param name="manifest">The manifest of the plugin that declares the field, whose locales hold the texts.</param>
    /// <returns>The DTO.</returns>
    public static DomainFieldDto ToDto(this PluginDomainField field, PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(manifest);

        return new DomainFieldDto(
            field.Key,
            PluginText.Resolve(manifest, field.Label),
            field.Type,
            field.Required,
            PluginText.Resolve(manifest, field.Description),
            PluginText.Resolve(manifest, field.Placeholder),
            field.Pattern,
            field.MinLength,
            field.MaxLength,
            field.Options
                .Select(option => new DomainFieldOptionDto(option.Value, PluginText.Resolve(manifest, option.Label)))
                .ToList());
    }
}
