using System.Text;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Parameters required to initiate an incoming domain transfer via a registrar provider.
/// </summary>
/// <param name="DomainName">Fully-qualified domain name to transfer (e.g. "example.com").</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy after the transfer completes.</param>
/// <param name="Contact">The registrant contact, built from the client's details, sent for every contact role.</param>
public record DomainTransferOrder(string DomainName, string EppCode, bool WhoisPrivacy, DomainContact Contact)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
