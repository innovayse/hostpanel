using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>A key the server's agent confirmed installed, with what the agent told about the server.</summary>
/// <param name="Key">The recorded key, in the installed state.</param>
/// <param name="HostKey">The server's public host key line (<c>ssh-ed25519 &lt;base64&gt;</c>), or null when the agent reports none.</param>
public sealed record SshKeyInstallation(SshKey Key, string? HostKey);
