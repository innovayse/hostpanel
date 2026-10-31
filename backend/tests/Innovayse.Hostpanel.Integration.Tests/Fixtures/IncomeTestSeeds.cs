using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One income scenario every income report test reads, seeded once per test class, so each report
/// is checked against the same movements and the expected figures are written down in one place.
/// </summary>
/// <remarks>
/// In the scenario year, in the base currency unless noted (the foreign rate is 0.0025, so 40,000
/// of it is 100 in the base):
/// <list type="bullet">
/// <item>Invoice 100, paid 31 January 23:59:59: January +100 (the last second of a month).</item>
/// <item>Invoice 200, paid 1 February 00:00:00, 50 refunded 10 March: February +200, March -50.</item>
/// <item>Invoice 80 covered by credit and marked paid 15 February: no income, one unit sold.</item>
/// <item>Invoice 40 imported as paid history on 5 April: April +40.</item>
/// <item>Foreign invoice 40,000, paid 10 February: February +100.</item>
/// <item>A credit top-up of 1,000 on 20 February: in the ledger, never income.</item>
/// <item>May orders: a pending one of 30, an accepted foreign one of 8,000 (20) on 20 May, a cancelled one of 999.</item>
/// </list>
/// So the year's income is 100, 300, -50, 40 for January to April and 390 in all.
/// </remarks>
public static class IncomeTestSeeds
{
    /// <summary>The second currency the scenario bills in.</summary>
    public const string ForeignCurrency = "AMD";

    /// <summary>What one unit of <see cref="ForeignCurrency"/> is worth in the base currency.</summary>
    public const decimal ForeignRate = 0.0025m;

    /// <summary>The gateway every seeded payment and refund goes through.</summary>
    private const string Gateway = "Card";

    /// <summary>Seeds the scenario into the year given.</summary>
    /// <param name="factory">The host whose database to seed.</param>
    /// <param name="year">The calendar year the movements fall in; one no other test of the class uses.</param>
    /// <returns>The ids and the base currency of the seeded scenario.</returns>
    public static async Task<IncomeScenario> SeedAsync(IntegrationTestFactory factory, int year)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();
        // Seeded history, not live payments: no mail, provisioning or renewal may follow from it.
        using var suppression = scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var baseCurrency = await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None);
        var foreign = await context.Currencies.FirstOrDefaultAsync(c => c.Code == ForeignCurrency, CancellationToken.None);
        if (foreign is null)
        {
            context.Currencies.Add(Currency.Create(ForeignCurrency, "051", string.Empty, " " + ForeignCurrency, 2, ForeignRate, isBase: false));
        }
        else
        {
            foreign.UpdateRate(ForeignRate);
        }

        var baseClient = Client.Create($"income-{Guid.NewGuid():N}", "Base", "Payer", $"base-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        var foreignClient = Client.Create($"income-{Guid.NewGuid():N}", "Foreign", "Payer", $"foreign-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        var group = ProductGroup.Create($"income-{Guid.NewGuid():N}", null);
        context.Clients.Add(baseClient);
        context.Clients.Add(foreignClient);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, $"Income plan {Guid.NewGuid():N}", null, null, null, "income", ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var baseService = ClientService.Create(baseClient.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        var foreignService = ClientService.Create(foreignClient.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(baseService);
        context.ClientServices.Add(foreignService);
        await context.SaveChangesAsync(CancellationToken.None);

        var code = baseCurrency.Code;

        var paidInJanuary = NewInvoice(baseClient.Id, code, 100m, baseService.Id, year);
        var partlyRefunded = NewInvoice(baseClient.Id, code, 200m, baseService.Id, year);
        var paidWithCredit = NewInvoice(baseClient.Id, code, 80m, baseService.Id, year);
        var importedHistory = NewInvoice(baseClient.Id, code, 40m, baseService.Id, year);
        var foreignInvoice = NewInvoice(foreignClient.Id, ForeignCurrency, 40_000m, foreignService.Id, year);
        context.Invoices.AddRange(paidInJanuary, partlyRefunded, paidWithCredit, importedHistory, foreignInvoice);

        // Saved first, so every ledger row below carries the id of the invoice it belongs to.
        await context.SaveChangesAsync(CancellationToken.None);

        var lastSecondOfJanuary = At(year, 2, 1).AddSeconds(-1);
        context.Transactions.Add(paidInJanuary.AddPayment(lastSecondOfJanuary, Gateway, Reference(), 100m, 0m, null));

        context.Transactions.Add(partlyRefunded.AddPayment(At(year, 2, 1), Gateway, Reference(), 200m, 0m, null));
        context.Transactions.Add(partlyRefunded.AddRefund(At(year, 3, 10), Gateway, Reference(), 50m, 0m, null));

        paidWithCredit.ApplyCredit(80m);
        paidWithCredit.MarkPaid(Reference(), At(year, 2, 15));

        importedHistory.RecordPaidHistory(Reference(), At(year, 4, 5));

        context.Transactions.Add(foreignInvoice.AddPayment(At(year, 2, 10), Gateway, Reference(), 40_000m, 0m, null));

        // Money added to the balance is the client's own, not income: it is only ever a ledger row.
        context.Transactions.Add(Transaction.Create(
            baseClient.Id, At(year, 2, 20), "Credit top-up", Reference(), null, Gateway, 1_000m, 0m, 0m, addedToCredit: true, code, TransactionSource.Manual));

        var pending = NewOrder(baseClient.Id, code, product, 30m, At(year, 5, 3));
        var accepted = NewOrder(foreignClient.Id, ForeignCurrency, product, 8_000m, At(year, 5, 20));
        accepted.Accept(At(year, 5, 20));
        var cancelled = NewOrder(baseClient.Id, code, product, 999m, At(year, 5, 21));
        cancelled.Cancel();
        context.Orders.AddRange(pending, accepted, cancelled);

        await context.SaveChangesAsync(CancellationToken.None);

        return new IncomeScenario(year, code, baseClient.Id, foreignClient.Id, product.Id);
    }

    /// <summary>The first instant of a UTC calendar day.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <returns>Midnight UTC of that day.</returns>
    public static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Raises an unpaid invoice with one service line, issued at the start of the scenario year.</summary>
    /// <param name="clientId">The client billed.</param>
    /// <param name="currency">The currency it bills in.</param>
    /// <param name="amount">The single line's price.</param>
    /// <param name="clientServiceId">The service the line bills.</param>
    /// <param name="year">The scenario year.</param>
    /// <returns>The invoice, not yet saved.</returns>
    private static Invoice NewInvoice(int clientId, string currency, decimal amount, int clientServiceId, int year)
    {
        var issued = At(year, 1, 1);
        var invoice = Invoice.Create(clientId, issued.AddDays(14), currency, issued);
        invoice.AddItem("Hosting", amount, 1, clientServiceId);
        invoice.Backdate(issued);
        return invoice;
    }

    /// <summary>Places a pending order with one line, dated in the scenario year.</summary>
    /// <param name="clientId">The client ordering.</param>
    /// <param name="currency">The currency the order is priced in.</param>
    /// <param name="product">The product ordered.</param>
    /// <param name="amount">The line's first payment.</param>
    /// <param name="placedAt">When the order was placed (UTC).</param>
    /// <returns>The order, not yet saved.</returns>
    private static Order NewOrder(int clientId, string currency, Product product, decimal amount, DateTimeOffset placedAt)
    {
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..20], clientId, Gateway, null, currency, placedAt);
        order.AddItem(product.Id, product.Name, BillingCycle.Monthly, amount, amount, null, null);
        order.Backdate(placedAt);
        return order;
    }

    /// <summary>A unique gateway reference.</summary>
    /// <returns>A reference no other seeded movement uses.</returns>
    private static string Reference() => $"tx-{Guid.NewGuid():N}";
}
