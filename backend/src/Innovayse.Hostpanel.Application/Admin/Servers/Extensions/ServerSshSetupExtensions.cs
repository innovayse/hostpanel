using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Extensions;

/// <summary>Builds <see cref="ServerSshSetupDto"/>.</summary>
public static class ServerSshSetupExtensions
{
    /// <summary>Path the anonymous bundle endpoint is served on.</summary>
    public const string BundlePath = "/api/public/ssh-agent/bundle.tar.gz";

    /// <summary>Default management user, as the installer names it.</summary>
    private const string DefaultAgentUsername = "hpsshkeys";

    /// <summary>
    /// Maps settings and the shipped bundles to the setup DTO. The profile is offered only when a bundle for it can be
    /// served: a profile whose plugin is gone or shipped no usable files has nothing to install, so the setup says it is
    /// not available (a null profile) and carries the built in bundle instead.
    /// </summary>
    /// <param name="settings">The server's settings, or null when none exist yet.</param>
    /// <param name="serverId">The server.</param>
    /// <param name="profile">The agent profile for its module, or null when the module has none.</param>
    /// <param name="bundles">The bundles this build can serve.</param>
    /// <param name="network">How managed servers see this HostPanel.</param>
    /// <returns>The DTO.</returns>
    public static ServerSshSetupDto ToSetupDto(
        this ServerSshSettings? settings, int serverId, string? profile, ISshAgentBundles bundles, SshAgentOptions network)
    {
        var bundle = profile is null ? null : bundles.For(profile);
        var offered = bundle is null ? null : profile;
        bundle ??= bundles.BuiltIn;
        var path = offered is null ? BundlePath : $"{BundlePath}?profile={Uri.EscapeDataString(offered)}";
        return new ServerSshSetupDto(
            serverId, offered, settings?.AgentPublicKey, settings?.AgentBinding,
            string.IsNullOrWhiteSpace(settings?.AgentUsername) ? DefaultAgentUsername : settings.AgentUsername,
            bundle.Version, bundle.Sha256, path, network.Addresses);
    }
}
