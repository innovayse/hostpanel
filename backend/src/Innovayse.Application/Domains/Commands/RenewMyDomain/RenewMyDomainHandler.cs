namespace Innovayse.Application.Domains.Commands.RenewMyDomain;

using Innovayse.Application.Common;
using Innovayse.Application.Domains.Common;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Resources;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Places a paid renewal order for one of the calling client's own domains, and never lets the
/// caller say whose domain it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is an order and not a direct registrar call.</b> This handler used to settle
/// ownership and then dispatch <c>RenewDomainCommand</c>, which calls the registrar immediately,
/// extends the expiry date and raises no invoice. That is correct for the admin route it was
/// written for -- a person has already decided the renewal is warranted, and billing is handled
/// outside it -- and wrong for a self-service one, where it hands out paid renewals for free to
/// anyone who owns a domain. It is the same objection that made domain transfer-in an order, and
/// the answer is the same one.
/// </para>
/// <para>
/// A renewal is already a thing this platform prices: the TLD table carries a renewal column
/// beside registration and transfer, and the public pricing page has published it all along.
/// What was missing was an order path behind it. <c>PlaceOrderHandler</c> now accepts an item
/// with <c>DomainAction = "renew"</c>, prices it from that column, raises the invoice, and
/// <c>FulfillPaidOrderHandler</c> dispatches <c>RenewDomainCommand</c> once the invoice is paid
/// -- refunding automatically if the registrar rejects it, the same as for a registration.
/// </para>
/// <para>
/// <b>Where the ownership boundary is.</b> Two checks, deliberately, and neither is redundant.
/// <see cref="IDomainOwnership.RequireOwnedByCallerAsync"/> runs here so the domain named on the
/// route is the caller's before an order number is spent on it; <c>FulfillPaidOrderHandler</c>
/// re-checks the owner when the invoice is paid, because fulfillment runs later from a payment
/// webhook and this check will not have travelled with the order line.
/// <see cref="ICurrentRequestContext.RequireUserId"/> is called as well -- not for decoration:
/// <c>PlaceOrderHandler</c> treats "no credential" as guest checkout and would register a
/// brand-new account for an anonymous caller, which is a sensible answer at a public checkout
/// and a nonsensical one behind <c>/api/me/domains</c>. In practice the ownership check gets
/// there first; this makes the guarantee independent of that ordering.
/// </para>
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="domains">Domain repository, for the name the order line has to carry.</param>
/// <param name="products">Product repository, for the product a domain order hangs off.</param>
/// <param name="caller">Who is renewing; the command does not say, and must not.</param>
/// <param name="bus">Wolverine bus, used to reach the order use case once ownership is settled.</param>
/// <param name="localizer">The refusal sentences, in the caller's own language.</param>
/// <param name="logger">Structured logger.</param>
public sealed class RenewMyDomainHandler(
    IDomainOwnership ownership,
    IDomainRepository domains,
    IProductRepository products,
    ICurrentRequestContext caller,
    IMessageBus bus,
    IStringLocalizer<ValidationMessages> localizer,
    ILogger<RenewMyDomainHandler> logger)
{
    /// <summary>Billing cycle recorded on a domain order line.</summary>
    /// <remarks>
    /// Domain lines are priced from the TLD table by action and year count, not from the
    /// product's cycle prices, so this is a label rather than an input to the total. It is the
    /// same string the public cart and the transfer-in route put on a domain item, and the order
    /// validator requires a non-empty one.
    /// </remarks>
    private const string DomainBillingCycle = "annually";

    /// <summary>Handles <see cref="RenewMyDomainCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this orders against the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new order and the invoice raised for it, for the client to pay.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the request carries no subject. Deliberately not allowed to fall through to
    /// <c>PlaceOrderHandler</c>, which would read the absence of a credential as guest checkout.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this installation sells no domain product, and by the order use case for a
    /// TLD or period it has no renewal price for.
    /// </exception>
    public async Task<PlaceOrderResultDto> HandleAsync(RenewMyDomainCommand cmd, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        await ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);

        // Loaded after the ownership check, never before it: the name is the one thing about
        // somebody else's domain this route could be used to read, and the check is what makes
        // reaching this line safe.
        var domain = await domains.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new DomainNotFoundException(cmd.DomainId);

        var domainProduct = await FindDomainProductAsync(ct);

        logger.LogInformation(
            "Placing a domain renewal order for domain {DomainId} on behalf of subject {UserId}.",
            cmd.DomainId, userId);

        // Every registration field is null on purpose. This route is behind the Client role, so
        // there is a credential; PlaceOrderHandler resolves the account from it and the guest
        // branch is unreachable. Passing names or an email here would be inventing a second way
        // to say who is ordering, which is the thing being avoided.
        return await bus.InvokeAsync<PlaceOrderResultDto>(
            new PlaceOrderCommand(
                FirstName: null,
                LastName: null,
                Email: null,
                Password: null,
                Phone: null,
                PaymentMethod: cmd.PaymentMethod,
                Items:
                [
                    new PlaceOrderItemDto(
                        ProductId: domainProduct.Id,
                        BillingCycle: DomainBillingCycle,
                        Domain: domain.Name,
                        Hostname: null,
                        DomainAction: "renew",
                        EppCode: null,
                        Years: cmd.Years),
                ]),
            ct);
    }

    /// <summary>
    /// Finds the active product a domain order line hangs off.
    /// </summary>
    /// <remarks>
    /// Keyed on <see cref="ProductType.Domain"/>, which is what the type is for -- the same
    /// lookup <c>TransferMyDomainHandler</c> makes, and deliberately not the public search
    /// page's guess at a product whose name contains "domain".
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain product to order against.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this installation has no active domain product, so there is nothing to sell a
    /// renewal as. A configuration gap rather than a caller error, but the caller still needs a
    /// sentence rather than a 500.
    /// </exception>
    private async Task<Product> FindDomainProductAsync(CancellationToken ct)
    {
        var active = await products.ListAsync(groupId: null, activeOnly: true, ct);

        return active.FirstOrDefault(p => p.Type == ProductType.Domain)
            ?? throw new InvalidOperationException(localizer["NoDomainProductConfigured"]);
    }
}
