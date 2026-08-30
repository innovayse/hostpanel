namespace Innovayse.Application.Domains.Commands.TransferMyDomain;

using Innovayse.Application.Orders.Commands.PlaceOrder;

/// <summary>Command for a client to transfer a domain in to their own account.</summary>
/// <remarks>
/// <para>
/// Carries no client id. Which account the transferred domain lands on is resolved from the
/// credential, so the scoping cannot be separated from the message the way an id in the body
/// can. The admin route that may transfer a domain in for any client dispatches
/// <c>TransferDomainCommand</c> directly, and that command <b>does</b> name a client -- which
/// is precisely why a client-facing route must not reach it.
/// </para>
/// <para>
/// It answers a <see cref="PlaceOrderResultDto"/> rather than a domain id because a transfer-in
/// is a purchase. See <see cref="TransferMyDomainHandler"/> for why the registrar is not called
/// until the invoice is paid.
/// </para>
/// </remarks>
/// <param name="DomainName">Fully-qualified domain name to transfer in.</param>
/// <param name="EppCode">Authorization/EPP code obtained from the losing registrar.</param>
/// <param name="Years">Registration period to buy with the transfer (1-10).</param>
/// <param name="PaymentMethod">Payment gateway module the client chose to pay the invoice with.</param>
public sealed record TransferMyDomainCommand(
    string DomainName,
    string EppCode,
    int Years,
    string PaymentMethod);
