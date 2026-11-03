using Innovayse.Hostpanel.Application.Plugins.Common.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>What <see cref="PluginCallGate"/> decided, with what it found on the way.</summary>
/// <param name="Outcome">The decision.</param>
/// <param name="Manifest">The plugin's manifest, once the plugin is known; otherwise <see langword="null"/>.</param>
/// <param name="Route">The declared route, once the request matched one; otherwise <see langword="null"/>.</param>
/// <param name="ClientId">The account a client call acts for, once it is known; otherwise <see langword="null"/>.</param>
/// <param name="Permissions">The permissions the client caller holds on the account; none for an admin call or before the account is known.</param>
/// <param name="Target">The matched route ready to run, only when <paramref name="Outcome"/> is <see cref="PluginCallGateOutcome.Admitted"/>.</param>
public sealed record PluginCallGateResult(
    PluginCallGateOutcome Outcome,
    PluginManifest? Manifest = null,
    PluginRoute? Route = null,
    int? ClientId = null,
    PluginTarget? Target = null,
    ClientPermission Permissions = ClientPermission.None)
{
    /// <summary>Gets a value indicating whether the call may go on.</summary>
    public bool IsAdmitted => Outcome == PluginCallGateOutcome.Admitted;
}
