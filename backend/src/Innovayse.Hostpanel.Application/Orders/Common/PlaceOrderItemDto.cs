using System.Text;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>DTO representing a single item in a place-order request.</summary>
/// <param name="ProductId">FK to the product being ordered; serialized as <c>productId</c>.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="Domain">Optional domain name for hosting or domain registration items.</param>
/// <param name="Hostname">Optional hostname for VPS/server products.</param>
/// <param name="DomainAction">
/// The domain action. Null for hosting items. A renew line names a domain this client already
/// owns; the other two name one they are acquiring.
/// </param>
/// <param name="EppCode">EPP authorization code for domain transfers.</param>
/// <param name="Years">Domain registration/transfer/renewal period in years (1 to 10).</param>
public sealed record PlaceOrderItemDto(
    int ProductId,
    BillingCycle BillingCycle,
    string? Domain,
    string? Hostname,
    DomainAction? DomainAction = null,
    string? EppCode = null,
    int? Years = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
