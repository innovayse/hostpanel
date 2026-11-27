using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetActiveDomainProduct;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.TransferMyDomain;

/// <summary>
/// Places a paid transfer-in order for a domain the calling client is bringing to this
/// registrar, and never lets the caller say whose account it lands on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is an order and not a direct registrar call.</b> The obvious mirror of
/// <c>RenewMyDomainHandler</c> would be to settle the caller and dispatch
/// <c>TransferDomainCommand</c> straight away. That command calls the registrar immediately and
/// records a price on the new domain without ever raising an invoice, which is correct for the
/// admin route it was written for, where a person has already decided the transfer is warranted,
/// and wrong for a self-service one, where it would hand out transfers for free.
/// </para>
/// <para>
/// A transfer-in is already a thing this platform sells: <c>PlaceOrderCommand</c> accepts an
/// item with <see cref="DomainAction.Transfer"/>, prices it from the TLD table, raises the invoice,
/// and <c>FulfillPaidOrderHandler</c> dispatches <c>TransferDomainCommand</c> once that invoice
/// is paid; refunding automatically if the registrar rejects it. So the shared use case this
/// handler reaches for is the order, not the registrar, and everything downstream of payment is
/// the code the public checkout already runs.
/// </para>
/// <para>
/// <b>Where the ownership boundary is.</b> <see cref="PlaceOrderCommand"/> carries no client id
/// either; it resolves the account from the credential inside its own handler. This one calls
/// <see cref="ICurrentUserService.RequireUserId"/> before dispatching anyway, and not for
/// decoration: <c>PlaceOrderHandler</c> treats "no credential" as guest checkout and would
/// register a brand-new account for an anonymous caller. That is a sensible answer at a public
/// checkout and a nonsensical one behind <c>/api/me/domains</c>, so this refuses instead.
/// </para>
/// </remarks>
public sealed class TransferMyDomainHandler
{
    /// <summary>Billing cycle recorded on a domain order line.</summary>
    /// <remarks>
    /// Domain lines are priced from the TLD table by action and year count, not from the
    /// product's cycle prices, so this is a label rather than an input to the total. It is the
    /// same cycle the public cart puts on a domain item.
    /// </remarks>
    private const BillingCycle DomainBillingCycle = BillingCycle.Annual;

    /// <summary>Who is transferring; the command does not say, and must not.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Wolverine bus, used to find the domain product and to reach the order use case once the caller is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<TransferMyDomainHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="caller">Who is transferring; the command does not say, and must not.</param>
    /// <param name="bus">Wolverine bus, used to find the domain product and to reach the order use case once the caller is settled.</param>
    /// <param name="logger">Structured logger.</param>
    public TransferMyDomainHandler(
        ICurrentUserService caller,
        IMessageBus bus,
        ILogger<TransferMyDomainHandler> logger)
    {
        _caller = caller;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles <see cref="TransferMyDomainCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this orders against the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new order and the invoice raised for it, for the client to pay, or
    /// <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c> when this installation sells no domain product.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the request carries no subject. Deliberately not allowed to fall through to
    /// <c>PlaceOrderHandler</c>, which would read the absence of a credential as guest checkout.
    /// </exception>
    public async Task<Result<PlaceOrderResultDto>> HandleAsync(TransferMyDomainCommand cmd, CancellationToken ct)
    {
        var userId = _caller.RequireUserId();

        var domainProductOutcome = await _bus.InvokeAsync<Result<DomainProductDto>>(new GetActiveDomainProductQuery(), ct);
        if (domainProductOutcome.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(domainProductOutcome.ErrorCode!);
        }

        _logger.LogInformation(
            "Placing a domain transfer-in order for '{DomainName}' on behalf of subject {UserId}.",
            cmd.DomainName, userId);

        // Every registration field is null on purpose. This route is behind the Client role, so
        // there is a credential; PlaceOrderHandler resolves the account from it and the guest
        // branch is unreachable. Passing names or an email here would be inventing a second way
        // to say who is ordering, which is the thing being avoided.
        return await _bus.InvokeAsync<Result<PlaceOrderResultDto>>(
            new PlaceOrderCommand(
                FirstName: null,
                LastName: null,
                Email: null,
                Password: null,
                PhoneNumber: null,
                PaymentMethod: cmd.PaymentMethod,
                Items:
                [
                    new PlaceOrderItemDto(
                        ProductId: domainProductOutcome.Value!.Id,
                        BillingCycle: DomainBillingCycle,
                        Domain: cmd.DomainName,
                        Hostname: null,
                        DomainAction: DomainAction.Transfer,
                        EppCode: cmd.EppCode,
                        Years: cmd.Years,
                        ExtendedAttributes: cmd.ExtendedAttributes),
                ]),
            ct);
    }
}
