using System.Text;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;

/// <summary>
/// Verifies the hosted-gateway payment on the invoice an order was billed on and, when the
/// gateway reports it paid, settles the invoice and fulfills the order.
/// </summary>
/// <remarks>
/// The order-shaped way in to <c>CompleteGatewayPaymentCommand</c>, for the checkout return leg
/// where the payer's browser comes back knowing only the order it started from.
/// </remarks>
/// <param name="OrderId">The order whose gateway session should be verified.</param>
/// <param name="PaymentToken">
/// The order's payment token, proving the caller is the payer this order was handed to.
/// Checkout is open to guests, so there is no credential to authorise against; see
/// <see cref="Innovayse.Hostpanel.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
/// <param name="ReturnParameters">The request the payer came back from the gateway with, handed on unchanged; see <c>CompleteGatewayPaymentCommand</c>.</param>
public sealed record CompleteOrderGatewayPaymentCommand(
    int OrderId,
    string? PaymentToken,
    IReadOnlyList<PaymentReturnParameterDto>? ReturnParameters = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
