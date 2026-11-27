using System.Text;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ModifyMyDomainContact;

/// <summary>Command for a client to modify the WHOIS registrant contact on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>ModifyDomainContactCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Contact">The updated registrant contact details.</param>
/// <param name="ExtendedAttributes">The registry's extra values by field key (see <c>GET /api/domains/fields</c>), checked against the fields the registrar plugin declares; they replace the ones stored, so an empty or absent set clears them. Printed masked, they can be personal data.</param>
public sealed record ModifyMyDomainContactCommand(
    int DomainId,
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
