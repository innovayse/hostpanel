using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>A freshly generated Ed25519 key pair, both halves as base64 of the raw 32 bytes.</summary>
/// <param name="PublicKey">The public half, sent to the registry.</param>
/// <param name="PrivateKey">The private seed; stored encrypted and never sent anywhere.</param>
public sealed record RegistryInstanceKeyPair(string PublicKey, string PrivateKey)
{
    /// <summary>Prints the pair without the private half, so no log line or exception message can carry it.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns>Always <see langword="true"/>: members are written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
