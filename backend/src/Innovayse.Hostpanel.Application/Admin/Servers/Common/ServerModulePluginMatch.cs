using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>Which installed plugin, if any, serves a server module for one kind of work.</summary>
/// <param name="Outcome">Whether no plugin, one plugin or several plugins declare the module.</param>
/// <param name="PluginId">The serving plugin's id when <paramref name="Outcome"/> is <see cref="ServerModulePluginOutcome.Served"/>; otherwise <see langword="null"/>.</param>
public sealed record ServerModulePluginMatch(ServerModulePluginOutcome Outcome, string? PluginId);
