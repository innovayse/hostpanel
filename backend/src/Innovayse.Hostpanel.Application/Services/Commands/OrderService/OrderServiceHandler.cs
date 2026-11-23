using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Products.Queries.CheckProductLimits;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.OrderService;

/// <summary>
/// Creates a pending <see cref="ClientService"/> record for the ordered product.
/// Provisioning is handled asynchronously by an event handler that listens
/// for <c>ClientServiceCreatedEvent</c>.
/// </summary>
public sealed class OrderServiceHandler
{
    /// <summary>The module's data context, the new record is added to.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the ordered product is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The currency the ordering client is billed in, for the fallback price.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, the new record is added to.</param>
    /// <param name="bus">The bus the ordered product is read through.</param>
    /// <param name="payerCurrency">The currency the ordering client is billed in, for the fallback price.</param>
    /// <param name="clock">Reads the current moment.</param>
    public OrderServiceHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IPayerCurrencyResolver payerCurrency,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The client, product, cycle and amounts to order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created client service id (or, for an order line that already has one, that service's id, also when a
    /// concurrent attempt of the line created it first), or <c>PRODUCT_NOT_FOUND</c> when the id names no
    /// product, <c>CLIENT_NOT_FOUND</c> when it names no client, or <c>PRODUCT_NOT_AVAILABLE</c> when the product is not active or an amount was
    /// left for this handler to price and the product has no price for that cycle in the
    /// client's currency, or <c>ORDER_LINE_SERVICE_CONFLICT</c> when the service already stored for the order line belongs to another
    /// client or product, or, for a service ordered with no order line, <c>ORDER_PRODUCT_SOLD_OUT</c> or
    /// <c>ORDER_PRODUCT_ONE_PER_CLIENT</c> when the product's cap on live services or its one-per-client rule refuses it, or,
    /// for a trial, <c>ORDER_TRIAL_ALREADY_USED</c> when the client has already used the trial of the product (also when a
    /// parallel start recorded it first); a refused trial records nothing.
    /// </returns>
    public async Task<Result<int>> HandleAsync(OrderServiceCommand cmd, CancellationToken ct)
    {
        if (cmd.OrderItemId is { } orderItemId)
        {
            // An earlier attempt of this order line already created its service (and may have died before recording
            // the line, or failed to activate it): that service is the line's, and a second one would be provisioned
            // and billed twice. Looked up before anything else, so the answer does not depend on the product still
            // being on sale.
            var existing = await FindServiceOfLineAsync(cmd, orderItemId, ct);
            if (existing is not null)
            {
                return existing;
            }
        }

        // The product belongs to the Products module, so it is read through its query.
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([cmd.ProductId]), ct);
        var product = products.Value?.FirstOrDefault();
        if (product is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        if (product.Status != ProductStatus.Active)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
        }

        if (!await _bus.ClientExistsAsync(cmd.ClientId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        // A client gets one trial of a product, ever: a recorded trial refuses another before anything is priced or
        // locked. The unique index on the record still decides a race between two starts below.
        if (cmd.Trial is not null && await TrialAlreadyUsedAsync(cmd, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
        }

        // The order-fulfilment callers pass the amounts snapshotted on the order line: zero
        // included, which is a free period and stays free. The client's own route passes null and
        // the amounts come from the product's price in that client's currency. Only a null amount
        // consults the stored price, so a product with no price in that currency is refused
        // rather than sold for nothing, and an accepted order never fails fulfilment over it.
        var needsPrice = cmd.FirstPaymentAmount is null || cmd.RecurringAmount is null;
        var cyclePriceOutcome = needsPrice
            ? await ResolveCyclePriceAsync(cmd, ct)
            : Result<decimal>.Success(0m);
        if (cyclePriceOutcome.IsFailure)
        {
            return Result<int>.FailureCode(cyclePriceOutcome.ErrorCode!);
        }

        var firstPayment = cmd.FirstPaymentAmount ?? cyclePriceOutcome.Value;
        var recurring = cmd.RecurringAmount ?? cyclePriceOutcome.Value;

        // A line of an order already took its place when the order was placed, so only a service ordered with no line is
        // checked, and a refusal there would never strand a paid order. The count and the save that takes the place
        // run under the product's lock, so a parallel order waits for this one and counts it. A trial always runs in a
        // transaction, so its service and the record of the trial are written together or not at all.
        var placeLimited = cmd.OrderItemId is null && Product.IsPlaceLimited(product.MaxActiveServices, product.OnePerClient);
        await using var transaction = placeLimited || cmd.Trial is not null ? await _context.BeginTransactionAsync(ct) : null;
        if (placeLimited)
        {
            await _context.LockProductPlacesAsync([product.Id], ct);
            var room = await _bus.InvokeAsync<Result>(new CheckProductLimitsQuery([product.Id], cmd.ClientId), ct);
            if (room.IsFailure)
            {
                return Result<int>.FailureCode(room.ErrorCode!);
            }
        }

        var now = _clock.UtcNow;
        var service = ClientService.CreateOrdered(
            cmd.ClientId, cmd.ProductId, cmd.BillingCycle, cmd.Domain, firstPayment, recurring, cmd.PaymentMethod, cmd.FirstInvoiceId, now, cmd.OrderItemId, cmd.Trial);

        _context.ClientServices.Add(service);
        var trialRecord = cmd.Trial is null ? null : ClientProductTrial.Create(cmd.ClientId, cmd.ProductId, now);
        if (trialRecord is not null)
        {
            _context.ClientProductTrials.Add(trialRecord);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException) when (cmd.OrderItemId is not null)
        {
            // Two attempts of the same line passed the lookup above and both created a service; the unique index let
            // one through. The line's service is the winner's, so this attempt drops its own row and answers with that one.
            _context.DiscardPendingChanges();
            var winner = await FindServiceOfLineAsync(cmd, cmd.OrderItemId.Value, ct);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }
        catch (ConcurrencyConflictException) when (trialRecord is not null)
        {
            // A parallel start of the same trial passed the check above and recorded it first; the unique index refused
            // this record, so this attempt drops its service and its record and the transaction ends without a commit.
            _context.DiscardPendingChanges();
            return Result<int>.FailureCode(HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
        }

        if (trialRecord is not null)
        {
            // The service's id exists only after the first save; the record is linked to it in the same transaction.
            trialRecord.AttachService(service.Id);
            await _context.SaveChangesAsync(ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return Result<int>.Success(service.Id);
    }

    /// <summary>
    /// Looks for the service an earlier attempt of an order line created, and checks it is the line's own: a service of
    /// another client or another product under the same line id means the data disagree, and reusing it would hand a
    /// client someone else's service.
    /// </summary>
    /// <param name="cmd">The order command, naming the client and the product the line is for.</param>
    /// <param name="orderItemId">The order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> when the line has no service yet; the service's id when it is the line's client's and product's;
    /// otherwise <c>ORDER_LINE_SERVICE_CONFLICT</c>.
    /// </returns>
    private async Task<Result<int>?> FindServiceOfLineAsync(OrderServiceCommand cmd, int orderItemId, CancellationToken ct)
    {
        var existing = await _context.ClientServices
            .AsNoTracking()
            .Where(s => s.OrderItemId == orderItemId)
            .Select(s => new { s.Id, s.ClientId, s.ProductId })
            .FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return null;
        }

        return existing.ClientId == cmd.ClientId && existing.ProductId == cmd.ProductId
            ? Result<int>.Success(existing.Id)
            : Result<int>.FailureCode(HostpanelErrorCodes.ORDER_LINE_SERVICE_CONFLICT);
    }

    /// <summary>Tells whether the client has already used the trial of the product.</summary>
    /// <param name="cmd">The order command, naming the client and the product.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a record of the client's trial of the product exists.</returns>
    private Task<bool> TrialAlreadyUsedAsync(OrderServiceCommand cmd, CancellationToken ct) =>
        _context.ClientProductTrials
            .AsNoTracking()
            .AnyAsync(t => t.ClientId == cmd.ClientId && t.ProductId == cmd.ProductId, ct);

    /// <summary>
    /// Asks the Products module for the product's stored price for the ordered cycle in the
    /// client's currency, so the pricing rule stays the product's own.
    /// </summary>
    /// <param name="cmd">The order command, naming the client and the cycle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The amount, or <c>PRODUCT_NOT_AVAILABLE</c> when the product has no such price.</returns>
    private async Task<Result<decimal>> ResolveCyclePriceAsync(
        OrderServiceCommand cmd, CancellationToken ct)
    {
        var currency = await _payerCurrency.ForClientAsync(cmd.ClientId, ct);
        var lookup = new ProductCyclePriceLookup(cmd.ProductId, currency.Code, cmd.BillingCycle);
        var prices = await _bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(
            new GetProductCyclePricesQuery([lookup]), ct);
        var price = prices.Value?.FirstOrDefault()?.Amount;
        return price is null
            ? Result<decimal>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE)
            : Result<decimal>.Success(price.Value);
    }
}
