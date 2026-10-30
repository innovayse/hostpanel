using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.SettleFreeInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;
using Innovayse.Hostpanel.Application.Domains.Queries.GetTldPricing;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProducts;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
namespace Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;

/// <summary>
/// Places a new order for an existing or newly registered client.
/// Checks and prices every line first, then has the Billing module raise the invoice and saves
/// the <see cref="Order"/> aggregate already linked to it, with product prices snapshotted into
/// its line items. Clients, products, currencies and invoices are
/// reached through their own modules' queries, commands and ports, never their tables.
/// </summary>
public sealed class PlaceOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Creates the account for guest checkout registration.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Role store, for granting the Client role to a newly provisioned guest.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>Wolverine message bus, for the other modules' queries and the invoice commands.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Who is ordering, and from where; the command says neither, and must not.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Resolves the account the caller orders for, owner or linked user; the command does not say, and must not.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>The currency the ordering client is billed in, stamped on the order's invoice.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Saves a signed-in caller's client record once, whichever writer gets there first.</summary>
    private readonly IClientRecordStore _clientRecords;

    /// <summary>Hands out order numbers atomically, so concurrent orders never share one.</summary>
    private readonly INumberSequence _numbers;

    /// <summary>How an order number is formatted from the sequence value.</summary>
    private readonly IOptions<OrderOptions> _orderOptions;

    /// <summary>
    /// Logs the precise reason a line item's price could not be resolved, which TLD, which
    /// currency mismatch, since the code the caller receives is deliberately generic.
    /// </summary>
    private readonly ILogger<PlaceOrderHandler> _logger;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="provisioning">Creates the account for guest checkout registration.</param>
    /// <param name="roles">Role store, for granting the Client role.</param>
    /// <param name="bus">Wolverine message bus, for the other modules' queries and the invoice command.</param>
    /// <param name="caller">Who is ordering, and from where; the command says neither, and must not.</param>
    /// <param name="callerClient">Resolves the account the caller orders for, owner or linked user; the command does not say, and must not.</param>
    /// <param name="payerCurrency">The currency the ordering client is billed in, stamped on the order's invoice.</param>
    /// <param name="clientRecords">Saves a signed-in caller's client record once, whichever writer gets there first.</param>
    /// <param name="numbers">Hands out order numbers atomically, so concurrent orders never share one.</param>
    /// <param name="orderOptions">How an order number is formatted from the sequence value.</param>
    /// <param name="logger">Logs the precise reason a line item's price could not be resolved.</param>
    /// <param name="clock">Reads the current moment.</param>
    public PlaceOrderHandler(
        IHostpanelDbContext context,
        IUserProvisioning provisioning,
        ISubjectRoleStore roles,
        IMessageBus bus,
        ICurrentUserService caller,
        ICallerClientResolver callerClient,
        IPayerCurrencyResolver payerCurrency,
        IClientRecordStore clientRecords,
        INumberSequence numbers,
        IOptions<OrderOptions> orderOptions,
        ILogger<PlaceOrderHandler> logger,
        IClock clock)
    {
        _context = context;
        _provisioning = provisioning;
        _roles = roles;
        _bus = bus;
        _caller = caller;
        _callerClient = callerClient;
        _payerCurrency = payerCurrency;
        _clientRecords = clientRecords;
        _numbers = numbers;
        _orderOptions = orderOptions;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <remarks>
    /// Everything that can refuse runs before anything is created: currency, payment method,
    /// products, prices and domain ownership. Only then is a guest account made and an order
    /// number taken. The invoice is raised next and the order saved already linked to it, so a
    /// failure leaves no Pending order without an invoice for the client to retry into a
    /// duplicate; a failure after the invoice exists cancels it through Billing.
    /// </remarks>
    /// <param name="cmd">The order to place.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new order's id and number, its invoice id, its payment token and the total and currency it
    /// is billed in, or
    /// <c>ORDER_PAYMENT_METHOD_UNAVAILABLE</c> when the checkout is not offering that method,
    /// <c>ORDER_CURRENCY_UNAVAILABLE</c> when a guest chose a currency this panel does not offer,
    /// <c>ORDER_GUEST_DETAILS_REQUIRED</c> when a guest checkout reached the handler without its
    /// registration fields, <c>ACCOUNT_EMAIL_EXISTS</c> when a guest's address already has an
    /// account, <c>PRODUCT_NOT_FOUND</c> or <c>PRODUCT_NOT_AVAILABLE</c> when a line item names a
    /// product that does not exist or is not active, <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c> when a
    /// product has no price for the requested currency and cycle, <c>ORDER_DOMAIN_PRICING_INVALID</c>
    /// when a domain line cannot be priced (see the private pricing helpers below for the causes
    /// that one code covers), <c>DOMAIN_NOT_FOUND</c> when a renewal names a domain the client does
    /// not hold, <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the signed-in caller's account may not order, or
    /// <c>ORDER_NUMBER_RANGE_EXHAUSTED</c> when the order number would outgrow its column.
    /// </returns>
    public async Task<Result<PlaceOrderResultDto>> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // The currency is settled before anything is created: an existing client is billed in
        // theirs, and a client this order is about to create in the checkout's choice, else the
        // base. Knowing it first is what lets a payment method be refused before an account
        // exists for it, and lets a guest who chose AMD be priced in AMD, not the base. Product
        // lines are read from the stored price in this currency; domain lines from the TLD table,
        // which the pricing query converts to it with the stored exchange rates.
        var now = _clock.UtcNow;
        var existingClient = await FindCallersClientAsync(ct);

        // A suspended or closed account orders nothing; refused before anything is priced or created.
        var mayOrder = existingClient?.Status.EnsureMayTransact() ?? Result.Success();
        if (mayOrder.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(mayOrder.ErrorCode!);
        }

        var currency = existingClient is null
            ? await ResolveNewClientCurrencyAsync(cmd, ct)
            : Result.Success((await _payerCurrency.ForClientAsync(existingClient.Id, ct)).Code);

        if (currency.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(currency.ErrorCode!);
        }

        var methodAvailable = await EnsurePaymentMethodAvailableAsync(cmd.PaymentMethod, currency.Value!, ct);
        if (methodAvailable.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(methodAvailable.ErrorCode!);
        }

        var priced = await PriceLinesAsync(cmd.Items, currency.Value!, ct);
        if (priced.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(priced.ErrorCode!);
        }

        var lines = priced.Value!;
        var owned = await EnsureRenewedDomainsOwnedAsync(lines, existingClient?.Id, ct);
        if (owned.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(owned.ErrorCode!);
        }

        var clientId = existingClient?.Id;
        if (clientId is null)
        {
            var created = await CreateClientAsync(cmd, currency.Value!, now, ct);
            if (created.IsFailure)
            {
                return Result<PlaceOrderResultDto>.FailureCode(created.ErrorCode!);
            }

            if (created.Value is null)
            {
                // The registration handler saved this caller's client record between our lookup
                // and our insert. Start over: the lookup now finds it, so the order is priced and
                // billed in that client's currency exactly as for any returning client.
                return await HandleAsync(cmd, ct);
            }

            clientId = created.Value;
        }

        // A signed-in caller who orders is a customer, whoever created their client row: this
        // handler, or the registration handler that can win the race to it. A sign-up through the
        // external sign-on holds no Client role, and without it every portal call answered 403 and
        // the portal told the customer who had just ordered that theirs was not a customer account.
        // Granting a role already held is harmless, so a returning client is granted it again.
        if (_caller.UserId is { } orderer)
        {
            await _roles.AddAsync(orderer, Role.Client, ct);
        }

        // Taken from a database sequence, not derived from the stored maximum: reading the maximum
        // and claiming max + 1 are two steps, so two concurrent orders claimed the same number and
        // the second hit the unique index. A sequence also never frees a number for reuse, so a
        // deleted order's number stays retired.
        var nextNumber = await _numbers.NextAsync(NumberKind.Order, ct);
        if (!_orderOptions.Value.Fits(nextNumber))
        {
            _logger.LogCritical(
                "Order number {Sequence} no longer fits the order number column with prefix '{Prefix}'; shorten the prefix.",
                nextNumber, _orderOptions.Value.NumberPrefix);
            return Result<PlaceOrderResultDto>.FailureCode(HostpanelErrorCodes.ORDER_NUMBER_RANGE_EXHAUSTED);
        }

        var order = Order.Create(
            _orderOptions.Value.FormatNumber(nextNumber), clientId.Value, cmd.PaymentMethod, _caller.IpAddress, currency.Value!, now);
        foreach (var line in lines)
        {
            var item = line.Item;
            order.AddItem(item.ProductId, line.ProductName, item.BillingCycle, line.Price, line.Price,
                item.Domain, item.Hostname, item.DomainAction, item.EppCode, item.Years);
        }

        // Raised before the order is saved: an invoice whose order then fails to save is cancelled
        // below, while an order saved without its invoice was a Pending order nobody could pay.
        var invoiceItems = lines.Select(line => new OrderInvoiceItemDto(DescribeLine(line), line.Price, 1, line.Snapshot)).ToList();
        var invoice = await _bus.InvokeAsync<Result<int>>(
            new CreateOrderInvoiceCommand(clientId.Value, currency.Value!, invoiceItems), ct);
        if (invoice.IsFailure)
        {
            return Result<PlaceOrderResultDto>.FailureCode(invoice.ErrorCode!);
        }

        order.LinkInvoice(invoice.Value);
        await SaveOrCancelInvoiceAsync(order, invoice.Value, ct);

        // Nothing is owed (a free plan): no gateway takes a payment of zero, so the invoice is
        // settled and the order fulfilled here, or it would wait forever for a payment.
        var total = lines.Sum(line => line.Price);
        if (total == 0)
        {
            await SettleFreeOrderAsync(order.Id, invoice.Value, ct);
        }

        return Result.Success(new PlaceOrderResultDto(
            order.Id, order.OrderNumber, invoice.Value, order.PaymentToken, total, currency.Value!));
    }

    /// <summary>
    /// Checks every line's product and prices it in the payer's currency, creating nothing.
    /// </summary>
    /// <param name="items">The checkout's lines.</param>
    /// <param name="currencyCode">ISO 4217 alpha code the order bills in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The priced lines, or <c>PRODUCT_NOT_FOUND</c>, <c>PRODUCT_NOT_AVAILABLE</c>,
    /// <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c> or <c>ORDER_DOMAIN_PRICING_INVALID</c>.
    /// </returns>
    private async Task<Result<IReadOnlyList<PricedOrderLine>>> PriceLinesAsync(
        IReadOnlyList<PlaceOrderItemDto> items, string currencyCode, CancellationToken ct)
    {
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<OrderableProductDto>>>(
            new GetOrderableProductsQuery(productIds, currencyCode), ct);
        if (products.IsFailure)
        {
            return Result<IReadOnlyList<PricedOrderLine>>.FailureCode(products.ErrorCode!);
        }

        var productMap = products.Value!.ToDictionary(p => p.Id);

        // The handler answers with a Result, so that is the type the bus is asked for; asking for
        // the bare DTO makes Wolverine look for a handler that returns one and fail the order.
        var hasDomainItems = items.Any(i => i.DomainAction is not null);
        var pricingOutcome = hasDomainItems
            ? await _bus.InvokeAsync<Result<TldPricingDto>>(new GetTldPricingQuery(currencyCode), ct)
            : null;
        if (pricingOutcome is { IsFailure: true })
        {
            return Result<IReadOnlyList<PricedOrderLine>>.FailureCode(pricingOutcome.ErrorCode!);
        }

        var lines = new List<PricedOrderLine>(items.Count);
        foreach (var item in items)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product))
            {
                return Result<IReadOnlyList<PricedOrderLine>>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
            }

            if (product.Status != ProductStatus.Active)
            {
                return Result<IReadOnlyList<PricedOrderLine>>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
            }

            // The validator requires a name on every domain line; a line that reached the handler
            // some other way without one is refused as unpriceable rather than dereferenced.
            var priceOutcome = item.DomainAction is null
                ? ResolvePrice(product, item.BillingCycle)
                : item.Domain is null
                    ? Result<decimal>.FailureCode(HostpanelErrorCodes.ORDER_DOMAIN_PRICING_INVALID)
                    : ResolveDomainPrice(pricingOutcome!.Value!, item.Domain, item.DomainAction.Value, item.Years ?? 1);
            if (priceOutcome.IsFailure)
            {
                return Result<IReadOnlyList<PricedOrderLine>>.FailureCode(priceOutcome.ErrorCode!);
            }

            // A domain line charges for a domain, not for the catalogue product it names, so it keeps no snapshot.
            var snapshot = item.DomainAction is null ? product.Fulfilment.ToSnapshot(item.BillingCycle) : null;
            lines.Add(new PricedOrderLine(item, product.Name, priceOutcome.Value, snapshot));
        }

        return Result<IReadOnlyList<PricedOrderLine>>.Success(lines);
    }

    /// <summary>
    /// Refuses a renewal line for a domain the ordering client does not hold.
    /// </summary>
    /// <remarks>
    /// Fulfillment checks the owner again, but only after the money was taken; checking here keeps
    /// a client from paying for a renewal that would then be refused. A client this order is about
    /// to create holds no domains, so any renewal line of theirs is refused.
    /// </remarks>
    /// <param name="lines">The priced lines.</param>
    /// <param name="clientId">The ordering client, or <see langword="null"/> when the order creates one.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>DOMAIN_NOT_FOUND</c> (one code, so the answer never tells whose a domain is).</returns>
    private async Task<Result> EnsureRenewedDomainsOwnedAsync(
        IReadOnlyList<PricedOrderLine> lines, int? clientId, CancellationToken ct)
    {
        var renewed = lines
            .Where(l => l.Item.DomainAction == DomainAction.Renew)
            .Select(l => l.Item.Domain!.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (renewed.Count == 0)
        {
            return Result.Success();
        }

        if (clientId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        var owned = await _bus.InvokeAsync<Result<IReadOnlyList<string>>>(
            new GetOwnedDomainNamesQuery(clientId.Value, renewed), ct);
        if (owned.IsFailure)
        {
            return Result.FailureCode(owned.ErrorCode!);
        }

        return renewed.All(name => owned.Value!.Contains(name))
            ? Result.Success()
            : Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>
    /// Saves the order already linked to its invoice; when the save fails, cancels the invoice
    /// through Billing so no invoice is left behind for an order that does not exist.
    /// </summary>
    /// <param name="order">The order to save.</param>
    /// <param name="invoiceId">The invoice raised for it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the order is saved; the save's own exception otherwise.</returns>
    private async Task SaveOrCancelInvoiceAsync(Order order, int invoiceId, CancellationToken ct)
    {
        try
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // Not the request's token: a cancelled request is exactly when the invoice must
            // still be withdrawn. The original failure is what the caller sees.
            var cancelled = await _bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoiceId), CancellationToken.None);
            if (cancelled.IsFailure)
            {
                _logger.LogCritical(
                    "Order save failed and its invoice {InvoiceId} could not be cancelled: {Code}. Cancel it by hand.",
                    invoiceId, cancelled.ErrorCode);
            }

            throw;
        }
    }

    /// <summary>
    /// Settles a nothing-owed order's invoice through Billing and fulfils the order.
    /// </summary>
    /// <remarks>
    /// The order is already placed and its invoice raised, so a refusal here is logged rather than
    /// returned: the client holds a valid order, and an operator can settle it from the admin.
    /// </remarks>
    /// <param name="orderId">The saved order.</param>
    /// <param name="invoiceId">Its zero-total invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once settlement and fulfillment have run.</returns>
    private async Task SettleFreeOrderAsync(int orderId, int invoiceId, CancellationToken ct)
    {
        var settled = await _bus.InvokeAsync<Result>(new SettleFreeInvoiceCommand(invoiceId), ct);
        if (settled.IsFailure)
        {
            _logger.LogError(
                "Free order {OrderId}: invoice {InvoiceId} could not be settled: {Code}.", orderId, invoiceId, settled.ErrorCode);
            return;
        }

        var fulfilled = await _bus.InvokeAsync<Result>(new FulfillPaidOrderCommand(orderId), ct);
        if (fulfilled.IsFailure)
        {
            _logger.LogError("Free order {OrderId} could not be fulfilled: {Code}.", orderId, fulfilled.ErrorCode);
        }
    }

    /// <summary>The invoice line text for an order line: the product, and for a domain line the domain it is for.</summary>
    /// <param name="line">The priced line.</param>
    /// <returns>The description the invoice carries.</returns>
    private static string DescribeLine(PricedOrderLine line) =>
        line.Item.DomainAction is not null && !string.IsNullOrWhiteSpace(line.Item.Domain)
            ? $"{line.ProductName} ({line.Item.Domain})"
            : line.ProductName;

    /// <summary>
    /// Refuses a payment method the checkout is not offering right now.
    /// </summary>
    /// <remarks>
    /// The method is a string the caller sends, and the list the checkout shows is only a
    /// suggestion to a client that can send anything. Without this check an operator who
    /// switched a gateway off in the admin integrations page still received orders against it:
    /// a bank-transfer order would sit pending for a transfer nobody expects, and a card order
    /// would fail later, on the first call that needed the gateway. The same query the checkout
    /// lists from decides here, so what is shown and what is accepted cannot drift apart.
    /// <para>
    /// The query is asked for the currency this order will bill in rather than the caller's. For
    /// a guest the two differ: the caller has no client yet, so the query would answer for the
    /// base, and a guest who chose AMD could place an order against a USD-only gateway that the
    /// payment start then refuses.
    /// </para>
    /// </remarks>
    /// <param name="paymentMethod">Module id the checkout sent.</param>
    /// <param name="currencyCode">ISO 4217 alpha code the order's invoice will bill in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <see cref="HostpanelErrorCodes.ORDER_PAYMENT_METHOD_UNAVAILABLE"/> when the
    /// method is not on offer.
    /// </returns>
    private async Task<Result> EnsurePaymentMethodAvailableAsync(
        string paymentMethod, string currencyCode, CancellationToken ct)
    {
        var available = await _bus.InvokeAsync<Result<IReadOnlyList<AvailablePaymentMethodDto>>>(
            new ListAvailablePaymentMethodsQuery(currencyCode), ct);

        if (available.IsFailure)
        {
            return Result.FailureCode(available.ErrorCode!);
        }

        if (!available.Value!.Any(m => string.Equals(m.Module, paymentMethod, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_PAYMENT_METHOD_UNAVAILABLE);
        }

        return Result.Success();
    }

    /// <summary>
    /// Finds the client record the caller's credential acts for, when it acts for one.
    /// </summary>
    /// <remarks>
    /// A signed-in caller orders for the account they own or are linked to. It is resolved from
    /// the credential, so there is no id anyone could send to order against another account. A
    /// caller the credential names but who acts for no account yet, and a guest, both come back as
    /// nothing here and get a record from <see cref="CreateClientAsync"/>.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's client brief, or <see langword="null"/> when they have none yet.</returns>
    private async Task<ClientBriefDto?> FindCallersClientAsync(CancellationToken ct)
    {
        var access = await _callerClient.ResolveAsync(ct);

        return access.ClientId is { } clientId
            ? await _bus.FindClientBriefAsync(clientId, withEmail: false, ct)
            : null;
    }

    /// <summary>
    /// Creates the client record the order belongs to, for a caller who has none. A caller the
    /// credential names gets a record against their existing account; anyone else creates a new
    /// account and <see cref="Client"/> record as part of guest checkout.
    ///
    /// <para>
    /// Guest checkout is a local-mode flow. Where an external sign-on provider owns the accounts this product
    /// cannot create one, so the provisioner refuses and says so: the customer has to
    /// sign in first, which is the only way their order can belong to anyone.
    /// </para>
    /// </summary>
    /// <param name="cmd">The place order command.</param>
    /// <param name="currencyCode">The currency the new record is billed in, already checked to be on offer.</param>
    /// <param name="now">The moment the order is being placed (UTC), read once by the handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new client's ID, <see langword="null"/> when a signed-in caller's record was saved by
    /// another writer first, <see cref="HostpanelErrorCodes.ACCOUNT_EMAIL_EXISTS"/> when a guest's
    /// address already has an account, or <see cref="HostpanelErrorCodes.ORDER_GUEST_DETAILS_REQUIRED"/> when
    /// an anonymous caller's registration fields are missing (the validator already requires these
    /// of an anonymous caller, so this is a defensive answer for a command that reached the
    /// handler some other way), or <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c> where
    /// the sign-on provider owns the accounts.
    /// </returns>
    private async Task<Result<int?>> CreateClientAsync(PlaceOrderCommand cmd, string currencyCode, DateTimeOffset now, CancellationToken ct)
    {
        // A caller the credential names but who has no client record yet is registered against
        // that credential, so a signed-in buyer's order lands on their own account.
        var subject = _caller.UserId;
        if (subject is not null)
        {
            // Their first order. The account already exists, they authenticated to get here,
            // so what is missing is only this product's client row, and the credential carries
            // everything it needs. Provisioning is deliberately not called: it creates accounts,
            // and where an external sign-on provider owns them it refuses, which is correct and not what is wanted
            // here.
            return await CreateClientForCallerAsync(subject, cmd, currencyCode, now, ct);
        }

        // Anonymous, and guest checkout is the only way left. The validator already requires all
        // four fields of a caller with no subject, so this refusal is reached only when something
        // dispatched the command without going through it.
        if (string.IsNullOrWhiteSpace(cmd.Email) || string.IsNullOrWhiteSpace(cmd.Password))
        {
            return Result<int?>.FailureCode(HostpanelErrorCodes.ORDER_GUEST_DETAILS_REQUIRED);
        }

        var created = await _provisioning.CreateAsync(
            new CreateUserInput(cmd.Email!, cmd.Password!, cmd.FirstName, cmd.LastName), ct);
        if (created.IsFailure)
        {
            return Result<int?>.FailureCode(created.ErrorCode!);
        }

        var userId = created.Value!;
        await _roles.AddAsync(userId, Role.Client, ct);

        var newClient = Client.Create(userId, cmd.FirstName!, cmd.LastName!, cmd.Email!, now);
        newClient.Update(cmd.FirstName!, cmd.LastName!, companyName: null, phone: cmd.PhoneNumber);
        newClient.SetCurrency(currencyCode);
        var written = await _clientRecords.AddOrGetAsync(newClient, ct);

        return Result.Success<int?>(written.Client.Id);
    }

    /// <summary>
    /// The currency a client record created by this order is billed in: the checkout's choice,
    /// else the base.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one moment a customer chooses their currency; after the first invoice it is
    /// locked (see <c>UpdateClientHandler</c>). It is recorded explicitly even when the choice
    /// is the base, so the row says what the client was billed in rather than leaving a later
    /// reader to work out what the base was at the time.
    /// </para>
    /// <para>
    /// Only a client this order creates is asked. An existing client's order carries the same
    /// field, because the checkout form sends it either way, and it is ignored: they already
    /// have a currency, and their invoices are in it.
    /// </para>
    /// </remarks>
    /// <param name="cmd">The order being placed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The ISO 4217 code of the configured currency to record on the new client and bill the
    /// order in, or <see cref="HostpanelErrorCodes.ORDER_CURRENCY_UNAVAILABLE"/> when the chosen
    /// code is not configured and enabled.
    /// </returns>
    private async Task<Result<string>> ResolveNewClientCurrencyAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        if (cmd.Currency is null)
        {
            return Result.Success((await _payerCurrency.BaseAsync(ct)).Code);
        }

        // Codes are stored in upper case; the checkout may send any case.
        var code = cmd.Currency.ToUpperInvariant();
        return await _bus.IsCurrencyOfferedAsync(code, ct)
            ? Result.Success(code)
            : Result<string>.FailureCode(HostpanelErrorCodes.ORDER_CURRENCY_UNAVAILABLE);
    }

    /// <summary>
    /// Creates the client row for a signed-in caller who does not have one yet.
    /// </summary>
    /// <remarks>
    /// Names come from the order when it carries them and from the credential otherwise. The
    /// checkout form asks a signed-in caller for neither, so in practice it is the credential;
    /// the command is preferred anyway because a caller who did type a billing name meant it.
    /// A client must have a first name, and the validator cannot require one here (whether the
    /// caller already has a row is a database question), so a caller whose credential carries
    /// no name is named after the local part of their email, the same default the account store
    /// uses; they can correct it in their profile. With no name and no email the order is refused.
    /// <para>
    /// The email is read from the credential rather than the command on purpose. A caller can
    /// put any address in a form field, and this row decides which account every later invoice
    /// and service belongs to.
    /// </para>
    /// </remarks>
    /// <param name="subject">The caller's subject, already known to have no client row.</param>
    /// <param name="cmd">The order being placed.</param>
    /// <param name="currencyCode">The currency the new row is billed in, already checked to be on offer.</param>
    /// <param name="now">The moment the order is being placed (UTC), read once by the handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new client's ID, <see langword="null"/> when another writer saved this user's record
    /// first, the registration handler runs on the bus after the register call returns, so a
    /// caller who orders at once can reach this before it has, or
    /// <see cref="HostpanelErrorCodes.ORDER_GUEST_DETAILS_REQUIRED"/> when neither the order, the
    /// credential nor the email supplies a first name.
    /// </returns>
    private async Task<Result<int?>> CreateClientForCallerAsync(
        string subject, PlaceOrderCommand cmd, string currencyCode, DateTimeOffset now, CancellationToken ct)
    {
        var (credentialFirst, credentialLast) = SplitDisplayName(await _caller.GetUserNameAsync(ct));
        var email = _caller.Email ?? cmd.Email ?? string.Empty;
        var firstName = FirstNonBlank(cmd.FirstName, credentialFirst) ?? Client.FirstNameFromEmail(email);
        if (firstName is null)
        {
            return Result<int?>.FailureCode(HostpanelErrorCodes.ORDER_GUEST_DETAILS_REQUIRED);
        }

        var client = Client.Create(
            subject,
            firstName,
            FirstNonBlank(cmd.LastName, credentialLast) ?? string.Empty,
            email,
            now);

        // The checkout's phone number is kept the same way a guest's is, so a signed-in buyer's
        // first order does not lose it.
        client.Update(client.FirstName, client.LastName, companyName: null, phone: cmd.PhoneNumber);

        // A new row, so this order is the one that chooses its currency: the same rule as a
        // guest's, because the caller is choosing at the same checkout.
        client.SetCurrency(currencyCode);

        var written = await _clientRecords.AddOrGetAsync(client, ct);

        return Result.Success<int?>(written.Created ? written.Client.Id : null);
    }

    /// <summary>Returns the first of two values that is neither null nor blank.</summary>
    /// <param name="preferred">The value to use when it says something.</param>
    /// <param name="fallback">Used when <paramref name="preferred"/> does not.</param>
    /// <returns>The chosen value, or <see langword="null"/> when neither says anything.</returns>
    private static string? FirstNonBlank(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? (string.IsNullOrWhiteSpace(fallback) ? null : fallback) : preferred;

    /// <summary>
    /// Splits a credential's display name into the first and last name the client row stores.
    /// </summary>
    /// <remarks>
    /// A display name is one string and this schema wants two, so the split is a guess: the
    /// first word is the given name and whatever follows is the family name. It is wrong for
    /// some names and there is no parse that is right for all of them. It is used only to fill
    /// the row at creation: the customer can correct both fields in their profile, and nothing
    /// but display reads them.
    /// </remarks>
    /// <param name="displayName">The credential's name claim, if it carries one.</param>
    /// <returns>The first and last name, each null when the name does not supply it.</returns>
    private static DisplayNameParts SplitDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new DisplayNameParts(null, null);
        }

        // A sign-on provider that knows no name sends the email as the display name; that is not a
        // name, and stored as one it greeted the customer as "Welcome back, a@b.c a@b.c". The row
        // then falls back to the email's local part like any caller whose credential has no name.
        if (displayName.Contains('@', StringComparison.Ordinal))
        {
            return new DisplayNameParts(null, null);
        }

        var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        return new DisplayNameParts(parts[0], parts.Length > 1 ? parts[1] : null);
    }

    /// <summary>Reads the stored price for a product in the payer's currency and cycle.</summary>
    /// <remarks>
    /// A product with no price in that currency is not for sale to that payer; the order refuses
    /// rather than converting from another currency, because a converted figure is not a price
    /// the operator set.
    /// </remarks>
    /// <param name="product">The product, with its prices in the payer's currency.</param>
    /// <param name="cycle">The requested billing cycle.</param>
    /// <returns>
    /// The amount, or a failed <see cref="Result{T}"/> carrying
    /// <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c> when the product has no price for that cycle in that
    /// currency.
    /// </returns>
    private Result<decimal> ResolvePrice(OrderableProductDto product, BillingCycle cycle)
    {
        var price = cycle == BillingCycle.Monthly ? product.Price.Monthly : product.Price.Annual;
        if (price is null)
        {
            _logger.LogWarning(
                "'{ProductName}' has no {Cycle} price in {CurrencyCode}; it cannot be ordered in that currency.",
                product.Name, cycle, product.Price.CurrencyCode);
            return Result<decimal>.FailureCode(HostpanelErrorCodes.ORDER_PRODUCT_PRICE_UNAVAILABLE);
        }

        return Result<decimal>.Success(price.Value);
    }

    /// <summary>
    /// Resolves the price for a domain registration or transfer from the TLD pricing table.
    /// </summary>
    /// <param name="tldPricing">Pre-fetched TLD pricing data, already converted to the payer's currency.</param>
    /// <param name="domainName">Fully-qualified domain name (e.g. "example.com").</param>
    /// <param name="action">The domain action.</param>
    /// <param name="years">Registration period in years.</param>
    /// <returns>
    /// The validated price for the domain operation, or a failed <see cref="Result{T}"/> carrying
    /// <c>ORDER_DOMAIN_PRICING_INVALID</c> when the name has no TLD, the TLD is not supported, the action is unrecognised, or there is no
    /// price for the period.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A TLD has one stored sell currency; the pricing query converts its figures to the payer's
    /// currency with the stored exchange rates, so the figure read here is already the amount the
    /// payer is charged and the invoice carries the same currency. The TLD's own sell currency
    /// therefore does not gate the order.
    /// </para>
    /// <para>
    /// "renew" prices from the TLD table's renewal column, which has always been loaded and
    /// published to the public pricing page and had no order path behind it. It is here so a
    /// client-initiated renewal is a purchase like the other two rather than a free call to the
    /// registrar; see <c>RenewMyDomainHandler</c>.
    /// </para>
    /// </remarks>
    private Result<decimal> ResolveDomainPrice(
        TldPricingDto tldPricing, string domainName, DomainAction action, int years)
    {
        var dotIndex = domainName.IndexOf('.');
        if (dotIndex < 0)
        {
            _logger.LogWarning("Invalid domain name on an order line: {DomainName}", domainName);
            return Result<decimal>.FailureCode(HostpanelErrorCodes.ORDER_DOMAIN_PRICING_INVALID);
        }

        var tld = domainName[(dotIndex + 1)..];

        var entry = tldPricing.Pricing.FirstOrDefault(e => e.Tld == tld);
        if (entry is null)
        {
            _logger.LogWarning("TLD '.{Tld}' is not supported for domain {Action}.", tld, action);
            return Result<decimal>.FailureCode(HostpanelErrorCodes.ORDER_DOMAIN_PRICING_INVALID);
        }

        var prices = action switch
        {
            DomainAction.Register => entry.Register,
            DomainAction.Transfer => entry.Transfer,
            DomainAction.Renew => entry.Renew,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "No price table for this domain action."),
        };

        var price = prices.FirstOrDefault(p => p.Years == years);
        if (price is null)
        {
            _logger.LogWarning("No pricing available for '.{Tld}' {Action} for {Years} year(s).", tld, action, years);
            return Result<decimal>.FailureCode(HostpanelErrorCodes.ORDER_DOMAIN_PRICING_INVALID);
        }

        return Result<decimal>.Success(price.Amount);
    }
}
