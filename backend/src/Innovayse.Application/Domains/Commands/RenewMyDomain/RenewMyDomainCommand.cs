namespace Innovayse.Application.Domains.Commands.RenewMyDomain;

using Innovayse.Application.Orders.Commands.PlaceOrder;

/// <summary>Command for a client to order a renewal of one of their own domain registrations.</summary>
/// <remarks>
/// <para>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin route that may act on any client's domain dispatches
/// <c>RenewDomainCommand</c> directly, and that command calls the registrar at once -- which is
/// precisely why a client-facing route must not reach it.
/// </para>
/// <para>
/// It answers a <see cref="PlaceOrderResultDto"/> rather than nothing at all because a renewal
/// is a purchase, exactly as a transfer-in is. See <see cref="RenewMyDomainHandler"/> for why the
/// registrar is not called until the invoice is paid, and why this command therefore has to name
/// a payment method.
/// </para>
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Years">Number of years to extend the registration by (1-10).</param>
/// <param name="PaymentMethod">Payment gateway module the client chose to pay the invoice with.</param>
public sealed record RenewMyDomainCommand(
    int DomainId,
    int Years,
    string PaymentMethod);
