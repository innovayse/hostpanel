using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>Result DTO returned after successfully placing an order.</summary>
/// <param name="OrderId">The newly created order's primary key.</param>
/// <param name="OrderNumber">The order's formatted number, for example <c>ORD-0042</c>, as shown everywhere else.</param>
/// <param name="InvoiceId">The newly created invoice's primary key.</param>
/// <param name="PaymentToken">
/// The token that authorises paying for this order. Order ids are sequential, so this is what
/// separates the payer from anyone who can count; it is returned here because a guest checkout
/// has no account the order could later be looked up from, and it must reach nobody else.
/// </param>
/// <param name="Total">
/// What the order is billed, in <paramref name="Currency"/>: the sum of the prices the backend
/// resolved for every line. The checkout compares it with the total it showed the payer, so a price
/// that changed in between is confirmed rather than charged silently.
/// </param>
/// <param name="Currency">ISO 4217 code of the currency the order is billed in.</param>
public sealed record PlaceOrderResultDto(
    int OrderId, string OrderNumber, int InvoiceId, string PaymentToken, decimal Total, string Currency)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
