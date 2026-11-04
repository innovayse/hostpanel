using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>The authorization code a gaining registrar asks for when a domain moves away.</summary>
/// <param name="EppCode">The EPP authorization code the registrar issued for the transfer.</param>
public record EppCodeDto(string EppCode)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
