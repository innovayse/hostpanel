using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RestartPluginHost;

/// <summary>
/// Restarts the host so the plugin changes staged by installs, updates and removals take effect: running requests finish
/// within the drain time, the process exits and the container's restart policy starts it again. Refused on the public demo.
/// </summary>
public sealed record RestartPluginHostCommand : IDemoRestricted;
