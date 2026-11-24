namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>What the server's agent answers when a key was installed.</summary>
/// <param name="HostKey">
/// The public host key line of the server (<c>ssh-ed25519 &lt;base64&gt;</c>, no comment) the agent reads from its
/// configured host key file, or <see langword="null"/> when the agent reports none (an older agent, no file).
/// </param>
public sealed record SshKeyReceipt(string? HostKey);
