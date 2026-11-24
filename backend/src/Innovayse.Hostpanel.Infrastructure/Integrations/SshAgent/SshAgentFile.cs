namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>One file of an SSH key agent installer bundle.</summary>
/// <param name="Path">The path inside the bundle, relative to its root and written with forward slashes.</param>
/// <param name="Bytes">The content of the file.</param>
internal sealed record SshAgentFile(string Path, byte[] Bytes);
