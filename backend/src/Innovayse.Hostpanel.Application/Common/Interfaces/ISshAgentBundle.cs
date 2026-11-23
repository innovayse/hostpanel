namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// One SSH key agent installer bundle: the agent, installer, uninstaller, profiles and helpers one profile needs, as one
/// deterministic <c>.tar.gz</c>. Its SHA256 is shown to the administrator so the one-command installer verifies what it
/// downloaded.
/// </summary>
public interface ISshAgentBundle
{
    /// <summary>Gets the agent version inside the bundle.</summary>
    public string Version { get; }

    /// <summary>Gets the lower-case hex SHA256 of <see cref="Content"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the bundle bytes.</summary>
    public ReadOnlyMemory<byte> Content { get; }
}
