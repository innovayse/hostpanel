using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>A record whose secret has a harmless name, so only the explicit attribute marks it.</summary>
/// <param name="Name">A plain member, printed as is.</param>
/// <param name="Code">A secret member the name rule cannot catch, marked with the attribute.</param>
internal sealed record SecretAttributeProbe(string Name, [property: SecretMember] string Code)
{
    /// <summary>Prints the record without its secret members.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
