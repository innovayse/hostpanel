using System.Text;

namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>A newly generated ED25519 management identity.</summary>
/// <remarks>
/// The generated <c>ToString</c> of a positional record writes every property, which would print the
/// private key into any log line or exception message that formats the value. <see cref="PrintMembers"/>
/// is overridden so the identity is described by its public key fingerprint only. Equality is untouched.
/// </remarks>
/// <param name="PrivateKey">OpenSSH-format private key. Stored encrypted, never returned by the API.</param>
/// <param name="PublicKey">The <c>ssh-ed25519 …</c> public key line handed to the installer.</param>
public sealed record SshIdentity(string PrivateKey, string PublicKey)
{
    /// <summary>What is printed when the public key is not a valid ED25519 line, so there is no fingerprint.</summary>
    private const string UnknownFingerprint = "unknown";

    /// <summary>Writes the public key fingerprint, never the private key, for the generated <c>ToString</c>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns>Always <see langword="true"/>: one member is written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        var fingerprint = SshPublicKey.TryParse(PublicKey, out var key, out _) ? key!.Fingerprint : UnknownFingerprint;
        builder.Append("PublicKeyFingerprint = ").Append(fingerprint);
        return true;
    }
}
