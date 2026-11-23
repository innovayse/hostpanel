using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>An <see cref="ISshAgentBundle"/> packed once from a set of files by <see cref="SshAgentBundleBuilder"/>.</summary>
public sealed class SshAgentBundle : ISshAgentBundle
{
    /// <summary>Packs the files and takes their SHA256.</summary>
    /// <param name="version">The agent version inside the bundle.</param>
    /// <param name="files">The files the bundle holds.</param>
    internal SshAgentBundle(string version, IEnumerable<SshAgentFile> files)
    {
        Version = version;
        Content = SshAgentBundleBuilder.Build(version, files);
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Content.Span));
    }

    /// <inheritdoc/>
    public string Version { get; }

    /// <inheritdoc/>
    public string Sha256 { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Content { get; }
}
