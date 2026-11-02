using Innovayse.Hostpanel.Application.Admin.Integrations.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The definition an integration slug declares: the built-in catalogue's, or that of the loaded
/// plugin with that id. The detail read and the save both resolve a slug here, so a slug the
/// detail page would answer 404 for can never be written either.
/// </summary>
public static class IntegrationFields
{
    /// <summary>Resolves the name and declared fields of an integration; a plugin's come from its manifest settings (<c>GetSettings()</c>).</summary>
    /// <param name="slug">The integration slug or plugin id.</param>
    /// <param name="plugins">The registry of loaded plugins.</param>
    /// <returns>The integration's definition, or <c>null</c> when the slug names neither a built-in integration nor a loaded plugin.</returns>
    public static IntegrationDefinition? Resolve(string slug, IPluginRegistry plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        if (IntegrationCatalogue.TryGet(slug, out var entry))
        {
            return new IntegrationDefinition(entry.Name, entry.Fields);
        }

        var manifest = plugins.GetLoadedManifests().FirstOrDefault(m => m.Id == slug);
        if (manifest is null)
        {
            return null;
        }

        return new IntegrationDefinition(manifest.Name, manifest.GetSettings().Select(s => s.ToFieldDefinition()).ToList());
    }
}
