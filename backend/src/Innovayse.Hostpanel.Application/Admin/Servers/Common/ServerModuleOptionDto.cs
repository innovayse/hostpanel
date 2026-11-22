using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>One control panel module an administrator can give a server.</summary>
/// <param name="Id">The id stored on the server, in the spelling the catalog keeps.</param>
/// <param name="Name">What the picker shows: the host's name for a built in module, the plugin's name for a plugin one.</param>
/// <param name="Source">Where the module comes from.</param>
public sealed record ServerModuleOptionDto(string Id, string Name, ServerModuleSource Source);
