using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ConfirmOrderPayment;

/// <summary>
/// Command to confirm a card payment for an order, mark the linked invoice as paid,
/// accept the order, and dispatch service creation for each order item.
/// </summary>
/// <param name="OrderId">The order primary key.</param>
/// <param name="PaymentIntentId">The card gateway payment intent identifier to verify.</param>
/// <param name="PaymentToken">
/// The order's payment token, proving the caller is the payer this order was handed to.
/// Checkout is open to guests, so there is no credential to authorise against; see
/// <see cref="Innovayse.Hostpanel.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
public sealed record ConfirmOrderPaymentCommand(int OrderId, string PaymentIntentId, string? PaymentToken)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
