using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.PurgePluginData;

/// <summary>
/// Deletes the data a removed plugin kept in the plugin store (<c>PluginDataEntries</c> and their scopes) and, through
/// the same step, anything else the host keeps for that plugin id. Refused while the plugin's package is still
/// installed, so the data of a plugin in use can never be wiped by mistake.
/// </summary>
/// <param name="Id">The plugin identifier whose kept data is deleted.</param>
public sealed record PurgePluginDataCommand(string Id) : IDemoRestricted;
