using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the sales pipeline around the invoices: quotes in every stage with their lines, billable items
/// (one-off and recurring, invoiced and waiting), and orders in every status, spread over the last year and
/// linked to invoices where the status allows. Figures and states come from fixed tables and positions, so
/// every reset reproduces the same pipeline.
/// </summary>
internal sealed class DemoSalesSeeder
{
    /// <summary>How many quotes are seeded.</summary>
    private const int QuoteCount = 66;

    /// <summary>How many billable items are seeded.</summary>
    private const int BillableItemCount = 66;

    /// <summary>How many orders are seeded.</summary>
    private const int OrderCount = 160;

    /// <summary>How many days a quote stays valid after it is written.</summary>
    private const int QuoteValidityDays = 30;

    /// <summary>The quote stages handed out in turn; every stage the entity can reach appears.</summary>
    private static readonly QuoteStage[] QuoteStages =
    [
        QuoteStage.Draft, QuoteStage.Delivered, QuoteStage.OnHold, QuoteStage.Accepted,
        QuoteStage.Lost, QuoteStage.Expired, QuoteStage.Dead,
    ];

    /// <summary>The services a quote proposes: description and dollar price of one unit.</summary>
    private static readonly DemoQuoteLine[] QuoteLines =
    [
        new("Website migration to managed hosting", 250m),
        new("Managed VPS setup and hardening", 180m),
        new("Monthly monitoring and patching", 75m),
        new("Email migration for ten mailboxes", 120m),
        new("Load balancer configuration", 300m),
        new("Staff training session", 90m),
    ];

    /// <summary>The one-off and recurring charges billable items are made of: description and dollar amount.</summary>
    private static readonly DemoBillableLine[] BillableLines =
    [
        new("Emergency restore from backup", 45m),
        new("Extra bandwidth block", 20m),
        new("Managed security review", 120m),
        new("Additional dedicated IP", 3m),
        new("Premium monitoring", 25m),
        new("Content delivery add-on", 15m),
        new("Migration assistance", 80m),
        new("Offsite archive storage", 12m),
    ];

    /// <summary>The order states handed out in turn; mostly accepted, with the odd pending, cancelled and flagged one.</summary>
    private static readonly OrderStatus[] OrderStatuses =
    [
        OrderStatus.Active, OrderStatus.Active, OrderStatus.Active, OrderStatus.Pending, OrderStatus.Active,
        OrderStatus.Cancelled, OrderStatus.Active, OrderStatus.Fraud, OrderStatus.Pending, OrderStatus.Active,
    ];

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoSalesSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the quotes, billable items and orders that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        if (clients.Count == 0)
        {
            return;
        }

        var invoiceIds = (await _db.Invoices.OrderBy(i => i.Id).Select(i => new { i.Id, i.ClientId }).ToListAsync(ct))
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Id).ToList());

        if (!await _db.Quotes.AnyAsync(ct))
        {
            await SeedQuotesAsync(clients, ct);
        }

        if (!await _db.BillableItems.AnyAsync(ct))
        {
            await SeedBillableItemsAsync(clients, invoiceIds, ct);
        }

        if (!await _db.Orders.AnyAsync(ct))
        {
            await SeedOrdersAsync(clients, invoiceIds, ct);
        }
    }

    /// <summary>Converts a dollar figure to the customer's currency.</summary>
    /// <param name="client">The customer.</param>
    /// <param name="usd">The price in dollars.</param>
    /// <returns>The same price in the customer's billing currency.</returns>
    private static decimal InCurrencyOf(Domain.Clients.Client client, decimal usd) =>
        client.Currency == DemoCatalogueSeeder.LocalCurrency ? DemoCatalogueSeeder.ToEuros(usd) : usd;

    /// <summary>Seeds quotes with two or three lines each, moved to the stage the table gives.</summary>
    /// <param name="clients">All customers, the demo client first.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedQuotesAsync(IReadOnlyList<Domain.Clients.Client> clients, CancellationToken ct)
    {
        for (var n = 0; n < QuoteCount; n++)
        {
            // The demo client gets the first four so its portal shows quotes in several stages.
            var client = clients[n < 4 ? 0 : (n - 3) % clients.Count];
            var stage = QuoteStages[n % QuoteStages.Length];

            // A quote is valid for thirty days from the day it was written. An expired one was written a month and a half or
            // more ago, every other one in the last three weeks, so no quote is dated after its own expiry.
            var written = _today.AddDays(-(stage == QuoteStage.Expired ? 45 + ((n * 7) % 50) : 1 + ((n * 3) % 20)));
            var expiry = written.AddDays(QuoteValidityDays);

            var quote = Quote.Create(
                client.Id, $"Proposal {n + 1}: hosting services for {client.CompanyName}", expiry,
                client.Currency ?? DemoCatalogueSeeder.BaseCurrency,
                "Prices exclude any domain fees.", "Thank you for considering us. This proposal covers the work below.",
                "Valid until the expiry date shown.", "Prepared by the sales team.");

            var lines = 2 + (n % 2);
            for (var i = 0; i < lines; i++)
            {
                var (description, price) = QuoteLines[(n + i) % QuoteLines.Length];
                quote.AddItem(description, InCurrencyOf(client, price), 1 + (i % 2), i == 1 ? 10m : 0m, i == 0);
            }

            switch (stage)
            {
                case QuoteStage.Delivered:
                    quote.Deliver();
                    break;
                case QuoteStage.OnHold:
                    quote.PutOnHold();
                    break;
                case QuoteStage.Accepted:
                    quote.Accept();
                    break;
                case QuoteStage.Lost:
                    quote.Deliver();
                    quote.MarkLost();
                    break;
                case QuoteStage.Expired:
                    quote.Deliver();
                    quote.MarkExpired();
                    break;
                case QuoteStage.Dead:
                    quote.MarkDead();
                    break;
                default:
                    break;
            }

            quote.Backdate(written);
            _db.Quotes.Add(quote);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds billable items: a third are recurring, every fourth is already on an invoice, the rest wait to be billed.</summary>
    /// <param name="clients">All customers, the demo client first.</param>
    /// <param name="invoiceIds">Each customer's invoice ids, oldest first.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedBillableItemsAsync(
        IReadOnlyList<Domain.Clients.Client> clients, IReadOnlyDictionary<int, List<int>> invoiceIds, CancellationToken ct)
    {
        for (var n = 0; n < BillableItemCount; n++)
        {
            var client = clients[n % clients.Count];
            var (description, amount) = BillableLines[n % BillableLines.Length];
            var currency = client.Currency ?? DemoCatalogueSeeder.BaseCurrency;
            var recurring = n % 3 == 0;

            var item = BillableItem.Create(
                client.Id,
                description,
                InCurrencyOf(client, amount),
                currency,
                recurring ? BillableItemType.Recurring : BillableItemType.OneTime,
                recurring ? (n % 2 == 0 ? RecurringPeriodNames.Monthly : RecurringPeriodNames.Annually) : null,
                recurring ? _today.AddDays(5 + ((n * 3) % 55)) : null);

            if (n % 4 == 1 && invoiceIds.TryGetValue(client.Id, out var ids) && ids.Count > 0)
            {
                item.MarkInvoiced(ids[n % ids.Count]);
            }

            _db.BillableItems.Add(item);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds orders over the last year, each for a hosting-type product and some with a domain registration too.</summary>
    /// <param name="clients">All customers, the demo client first.</param>
    /// <param name="invoiceIds">Each customer's invoice ids, oldest first.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedOrdersAsync(
        IReadOnlyList<Domain.Clients.Client> clients, IReadOnlyDictionary<int, List<int>> invoiceIds, CancellationToken ct)
    {
        var products = await _db.Products.Include(p => p.Prices)
            .Where(p => p.Type != ProductType.Domain && p.Status == ProductStatus.Active)
            .OrderBy(p => p.Id).ToListAsync(ct);
        var domainProduct = await _db.Products.OrderBy(p => p.Id).FirstOrDefaultAsync(p => p.Type == ProductType.Domain, ct);
        if (products.Count == 0)
        {
            return;
        }

        for (var n = 0; n < OrderCount; n++)
        {
            var client = clients[n % clients.Count];
            var currency = client.Currency ?? DemoCatalogueSeeder.BaseCurrency;
            var product = products[((n * 3) + 1) % products.Count];
            var cycle = n % 3 == 0 ? BillingCycle.Annual : BillingCycle.Monthly;
            var price = product.PriceFor(currency, cycle) ?? 0m;
            var status = OrderStatuses[n % OrderStatuses.Length];

            var order = Order.Create(
                $"DEMO-{1000 + n}", client.Id, PaymentMethodNames.Card, $"203.0.113.{10 + n}", currency, _today);
            order.AddItem(product.Id, product.Name, cycle, price, price, DemoSites.HostOf(client), null);
            if (n % 5 == 0 && domainProduct is not null)
            {
                var domainPrice = currency == DemoCatalogueSeeder.BaseCurrency ? 11.99m : 4600m;
                order.AddItem(
                    domainProduct.Id, domainProduct.Name, BillingCycle.Annual, domainPrice, domainPrice,
                    $"{DemoSites.LabelOf(client)}-order{n}.example", null, DomainAction.Register, null, 1);
            }

            if (invoiceIds.TryGetValue(client.Id, out var ids) && ids.Count > 0)
            {
                order.LinkInvoice(ids[n % ids.Count]);
            }

            switch (status)
            {
                case OrderStatus.Active:
                    order.Accept(_today);

                    // Demo history: these orders were fulfilled long ago, so their lines are not forgotten ones.
                    order.MarkLinesFulfilledFromHistory(_today);
                    break;
                case OrderStatus.Cancelled:
                    order.Cancel();
                    break;
                case OrderStatus.Fraud:
                    order.MarkFraud();
                    order.SetNotes("Flagged by payment screening: the billing country differs from the card country.");
                    break;
                default:
                    break;
            }

            // Spread across the year so the order report has a trend, oldest first.
            // An order is never older than the account that placed it.
            var placed = _today.AddDays(-(5 + (((OrderCount - n) * 13) % 350)));
            order.Backdate(placed > client.CreatedAt ? placed : client.CreatedAt.AddDays(1 + (n % 10)));
            _db.Orders.Add(order);
        }

        await _db.SaveChangesAsync(ct);
    }
}
