namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// What the administrator needs to install the SSH key agent on one server. Never carries the
/// private key. The admin panel builds the one-command installer from these values and its own
/// origin, so the command always downloads from the HostPanel the administrator is signed in to.
/// </summary>
/// <param name="ServerId">The server.</param>
/// <param name="Profile">Agent profile id the plugin serving the server's module ships, null when the module is unsupported or its profile is not available.</param>
/// <param name="PublicKey">The management public key to install, null until an identity is generated.</param>
/// <param name="Binding">The id the installer binds the agent to.</param>
/// <param name="AgentUsername">The management user the installer creates.</param>
/// <param name="BundleVersion">Agent version in the bundle this HostPanel serves.</param>
/// <param name="BundleSha256">SHA256 the installer command verifies before unpacking.</param>
/// <param name="BundlePath">Path of the bundle on this HostPanel.</param>
/// <param name="SourceAddresses">
/// Addresses this HostPanel connects to agents from, for the firewall and sshd lines; empty when
/// not configured.
/// </param>
public sealed record ServerSshSetupDto(
    int ServerId,
    string? Profile,
    string? PublicKey,
    Guid? Binding,
    string AgentUsername,
    string BundleVersion,
    string BundleSha256,
    string BundlePath,
    IReadOnlyList<string> SourceAddresses);
