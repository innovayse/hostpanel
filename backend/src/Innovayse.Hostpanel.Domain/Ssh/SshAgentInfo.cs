namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>What an agent reports about itself on ping.</summary>
/// <param name="Version">Agent version.</param>
/// <param name="Provider">The provider profile it runs (<c>cwp</c>, <c>generic-linux</c> and so on).</param>
/// <param name="Binding">The server record it was installed for, or null when the install predates bindings.</param>
/// <param name="HostKey">The server's public host key line (<c>ssh-ed25519 &lt;base64&gt;</c>), or null when the agent reports none.</param>
public sealed record SshAgentInfo(string Version, string? Provider, Guid? Binding, string? HostKey = null);
