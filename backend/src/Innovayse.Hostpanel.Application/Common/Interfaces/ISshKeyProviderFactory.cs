using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves the SSH key capability of a server from its <see cref="Server.Module"/> and its
/// <see cref="ServerSshSettings"/>. Answers <see langword="null"/>, explicitly unsupported,
/// for any module without an implementation, so existing provisioning providers need no change.
/// </summary>
public interface ISshKeyProviderFactory
{
    /// <summary>Whether the server's module has an SSH key implementation at all.</summary>
    /// <param name="module">The server module.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    public bool Supports(string module);

    /// <summary>
    /// The agent profile a supported module installs: the id the plugin that serves the module names in its manifest, used to
    /// build the installer command. Null for an unsupported module.
    /// </summary>
    /// <param name="module">The server module.</param>
    /// <returns>The profile name, or null.</returns>
    public string? AgentProfileFor(string module);

    /// <summary>Creates a provider bound to one server.</summary>
    /// <param name="target">The server and its SSH settings; the settings must be present and complete.</param>
    /// <returns>The provider, or <see langword="null"/> when the module is unsupported.</returns>
    public ISshKeyProvider? CreateFor(ServerSshTargetDto target);
}
