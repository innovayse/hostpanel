using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Orders.Commands.StartOrderGatewayPayment;

/// <summary>Starts a hosted-gateway payment for the invoice an order was billed on.</summary>
/// <remarks>
/// The order-shaped way in to the same work <c>StartGatewayPaymentCommand</c> does. Checkout knows
/// the order it just placed, not the invoice generated behind it, and resolving one from the other
/// is a lookup that belongs on this side of the HTTP edge rather than in the endpoint.
/// </remarks>
/// <param name="OrderId">The order to pay.</param>
/// <param name="Module">The payment plugin id (e.g. "bank-gateway").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
/// <param name="PaymentToken">
/// The order's payment token, proving the caller is the payer this order was handed to.
/// Checkout is open to guests, so there is no credential to authorise against; see
/// <see cref="Innovayse.Hostpanel.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
/// <param name="SavePaymentMethod">
/// Whether the payer asked to keep the payment method they pay with, so the host can charge it later. Needs a gateway
/// module that offers stored credentials; the consent evidence below is recorded with the payment.
/// </param>
/// <param name="ConsentTextVersion">
/// The version of the consent wording the payer ticked, as the portal showed it; kept as evidence. Required when
/// <paramref name="SavePaymentMethod"/> is set, and then the wire value of a published version
/// (<see cref="Innovayse.Hostpanel.Domain.Billing.Enums.ConsentTextVersion"/>); ignored otherwise.
/// </param>
/// <param name="IpAddress">The address of the request, set by the controller; not read from the body.</param>
/// <param name="UserAgent">The browser identification of the request, set by the controller; not read from the body.</param>
public sealed record StartOrderGatewayPaymentCommand(
    int OrderId,
    string Module,
    string ReturnUrl,
    string? PaymentToken,
    bool SavePaymentMethod = false,
    string? ConsentTextVersion = null,
    string? IpAddress = null,
    string? UserAgent = null)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
