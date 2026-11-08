using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.ClearRegistryPluginLicence;

/// <summary>Deletes the licence key stored for a registry plugin. Clearing a key that is not stored succeeds and changes nothing.</summary>
/// <param name="PluginId">The plugin id; set from the route.</param>
public sealed record ClearRegistryPluginLicenceCommand(string PluginId) : IDemoRestricted;
