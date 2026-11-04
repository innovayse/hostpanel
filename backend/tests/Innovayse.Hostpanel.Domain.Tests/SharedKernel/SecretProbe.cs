using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>A record shaped like the real ones: plain members, secret members and a flag named after a secret.</summary>
/// <param name="Name">A plain member, printed as is.</param>
/// <param name="Password">A secret member, masked.</param>
/// <param name="Token">A nullable secret member, masked when set.</param>
/// <param name="HasPassword">A flag that describes the secret and is not one.</param>
internal sealed record SecretProbe(string Name, string Password, string? Token, bool HasPassword)
{
    /// <summary>Prints the record without its secret members.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
