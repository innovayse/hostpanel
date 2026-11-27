using System.Text;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Parameters required to register a new domain via a registrar provider.
/// </summary>
/// <param name="DomainName">Fully-qualified domain name to register (e.g. "example.com").</param>
/// <param name="Years">Number of years to register the domain for.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy on registration.</param>
/// <param name="AutoRenew">Whether to enable automatic renewal at the registrar.</param>
/// <param name="Nameserver1">Primary nameserver hostname; null to use registrar defaults.</param>
/// <param name="Nameserver2">Secondary nameserver hostname; null to use registrar defaults.</param>
/// <param name="Contact">The registrant contact, built from the client's details, sent for every contact role.</param>
/// <param name="ExtendedAttributes">The registry's extra values by field key, already checked against the registrar plugin's fields; absent for none. Printed masked.</param>
public record DomainRegistrationOrder(
    string DomainName,
    int Years,
    bool WhoisPrivacy,
    bool AutoRenew,
    string? Nameserver1,
    string? Nameserver2,
    DomainContact Contact,
    [property: SecretMember] IReadOnlyDictionary<string, string>? ExtendedAttributes = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
