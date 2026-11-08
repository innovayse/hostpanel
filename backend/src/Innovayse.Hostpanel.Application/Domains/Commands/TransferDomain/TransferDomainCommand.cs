using System.Text;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;

/// <summary>Command to initiate an incoming domain transfer for a client.</summary>
/// <param name="ClientId">The client requesting the transfer.</param>
/// <param name="DomainName">Fully-qualified domain name to transfer.</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy after the transfer completes.</param>
/// <param name="FirstPaymentAmount">One-time transfer cost to record on the domain.</param>
/// <param name="RecurringAmount">Recurring renewal price to record on the domain.</param>
/// <param name="PaymentMethod">Payment method label to record on the domain; null if not set.</param>
/// <param name="Years">Registration period bought with the transfer, in years (1-10).</param>
/// <param name="PriceCurrency">ISO 4217 code of the currency the amounts are in (the paid order's currency); null bills in the client's billing currency.</param>
/// <param name="OrderId">
/// The order whose line transfers the domain in, set only by order fulfilment and stored on the domain so a retry of the
/// line can tell the transfer it made from a domain the client holds from elsewhere. Never bound from a request body.
/// </param>
public sealed record TransferDomainCommand(
    int ClientId,
    string DomainName,
    string EppCode,
    bool WhoisPrivacy,
    decimal FirstPaymentAmount = 0,
    decimal RecurringAmount = 0,
    string? PaymentMethod = null,
    int Years = 1,
    string? PriceCurrency = null,
    [property: JsonIgnore] int? OrderId = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
