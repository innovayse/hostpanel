using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports order records, deduplicated by order number. A service line is linked to the local product
/// imported for its source product id; a domain or service addon line has no catalogue product here
/// and is not stored as an order item (the domain itself is imported by <see cref="DomainsImporter"/>).
/// </summary>
public sealed class OrdersImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Maps the source product ids the order lines carry to the products imported here.</summary>
    private readonly MigrationProductLookup _productLookup;

    /// <summary>Decides the currency an order with no currency of its own is in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the moment each order is imported.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="OrdersImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="productLookup">Maps the source product ids the order lines carry to the products imported here.</param>
    /// <param name="payerCurrency">Decides the currency an order with no currency of its own is in.</param>
    /// <param name="clock">Reads the moment each order is imported.</param>
    public OrdersImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        MigrationProductLookup productLookup,
        IPayerCurrencyResolver payerCurrency,
        IClock clock)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _productLookup = productLookup;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Orders;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Read once per run: every service line names its product by the source id, and this map
        // turns that id into the product imported here.
        var sourceProducts = await _productLookup.LoadSourceProductMapAsync(job, ct);
        await _runner.RunAsync(
            new MigrationRecordRun<OrderRecord>(
                job, EntityType, "orders", (rec, token) => ImportOrderAsync(rec, sourceProducts, token)),
            ct);
    }

    /// <summary>Imports one plugin-side order record.</summary>
    /// <param name="rec">The order record read from the source plugin.</param>
    /// <param name="sourceProducts">The local product per source product id, for the record's service lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the order was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportOrderAsync(
        OrderRecord rec, IReadOnlyDictionary<int, Product> sourceProducts, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        var existingOrder = await _context.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == rec.OrderNumber, ct);
        if (existingOrder is not null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // A domain or service addon line carries no source product: it has no catalogue product here,
        // and an order line always belongs to one, so only service lines become order items. An order
        // made of such lines alone would be stored as an empty order, which it never was.
        var lines = rec.Items ?? [];
        if (lines.Count > 0 && lines.TrueForAll(item => item.ProductId is null))
        {
            return RecordImportResult.Skipped(MigrationSkipReason.NoProductLines);
        }

        // The source record carries no currency, so the order is in what the migrated client is billed in.
        var currency = (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code;
        var now = _clock.UtcNow;
        var order = Order.Create(rec.OrderNumber, clientId.Value, rec.PaymentMethod, null, currency, now);

        foreach (var item in lines)
        {
            if (item.ProductId is not { } sourceProductId)
            {
                continue;
            }

            // The order has not been added to the context yet, so skipping here leaves nothing behind.
            // No fallback to another product: a line linked to the wrong product would report and
            // provision as that product without anyone noticing.
            if (!sourceProducts.TryGetValue(sourceProductId, out var product))
            {
                return RecordImportResult.Skipped(MigrationSkipReason.ProductNotFound);
            }

            if (!BillingCycleParser.TryParse(item.BillingCycle, out var billingCycle))
            {
                return RecordImportResult.Skipped(MigrationSkipReason.BillingCycleUnsupported);
            }

            order.AddItem(product.Id, item.ProductName, billingCycle,
                item.FirstPaymentAmount, item.RecurringAmount,
                item.Domain, item.Hostname);
        }

        switch (rec.Status)
        {
            case SourceOrderStatuses.Active:
                // The source delivered the lines long ago: accepted and fulfilled from history, so they never read as
                // not run yet, and no run here starts them again.
                order.Accept(now);
                order.MarkLinesFulfilledFromHistory(rec.CreatedAt ?? now);
                break;
            case SourceOrderStatuses.Cancelled:
                order.Cancel();
                break;
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
