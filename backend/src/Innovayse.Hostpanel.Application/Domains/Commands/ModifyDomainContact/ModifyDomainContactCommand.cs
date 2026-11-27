using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ModifyDomainContact;

/// <summary>
/// Command to modify the WHOIS registrant contact details at the registrar. Carries the contact
/// fields flat rather than as a nested <c>Contact</c> object: the admin app's
/// <c>ClientDomainDetailView</c> posts them at the top level of the body, and a nested wrapper here
/// would not bind them.
/// </summary>
/// <param name="DomainId">
/// Primary key of the domain whose contact to modify. Nullable so the route id can overlay it: a
/// non-nullable member here would make the body required before <c>with { DomainId = id }</c>
/// ever ran.
/// </param>
/// <param name="FirstName">Registrant first name.</param>
/// <param name="LastName">Registrant last name.</param>
/// <param name="Organization">Organization or company name; null when the registrant has none.</param>
/// <param name="Email">Contact email address.</param>
/// <param name="Phone">Contact phone number.</param>
/// <param name="Address1">Primary street address.</param>
/// <param name="Address2">Secondary address line; null when there is none.</param>
/// <param name="City">City name.</param>
/// <param name="State">State or province.</param>
/// <param name="PostalCode">Postal or ZIP code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="ExtendedAttributes">The registry's extra values by field key (see <c>GET /api/domains/fields</c>), checked against the fields the registrar plugin declares; they replace the ones stored, so an empty or absent set clears them. Printed masked, they can be personal data.</param>
public sealed record ModifyDomainContactCommand(
    int? DomainId,
    string FirstName,
    string LastName,
    string? Organization,
    string Email,
    string Phone,
    string Address1,
    string? Address2,
    string City,
    string State,
    string PostalCode,
    string Country,
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
