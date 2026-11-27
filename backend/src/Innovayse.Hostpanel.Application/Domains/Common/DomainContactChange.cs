using System.Text;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Replaces the WHOIS registrant contact details of a domain at the registrar.
/// </summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Contact">The updated contact details.</param>
/// <param name="ExtendedAttributes">The registry's extra values by field key, already checked against the registrar plugin's fields; absent for none. Printed masked.</param>
public sealed record DomainContactChange(
    DomainRef Domain,
    DomainContact Contact,
    [property: SecretMember] IReadOnlyDictionary<string, string>? ExtendedAttributes = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
