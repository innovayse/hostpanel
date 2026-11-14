using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.ProcessBillableItemsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeds billable items through the real invoicing run, for the tests of deleting the invoice an item
/// was put on.
/// </summary>
public static class BillableItemSeeds
{
    /// <summary>
    /// Seeds a client with one due one-time item, lets the invoicing run put it on an invoice, and
    /// cancels that invoice so it may be deleted.
    /// </summary>
    /// <param name="factory">The host to seed through.</param>
    /// <returns>The item and the cancelled invoice it sits on.</returns>
    public static async Task<ItemOnInvoice> SeedItemOnACancelledInvoiceAsync(IntegrationTestFactory factory)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        int itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var item = BillableItem.Create(
                clientId, "Consulting", 40m, "USD", BillableItemType.OneTime, nextDueDate: DateTimeOffset.UtcNow.AddDays(-1));
            context.BillableItems.Add(item);
            await context.SaveChangesAsync(CancellationToken.None);
            itemId = item.Id;
        }

        await RunInvoicingAsync(factory);
        var invoiceId = (await ReadItemAsync(factory, itemId)).InvoiceId!.Value;

        using var cancelScope = factory.Services.CreateScope();
        var bus = cancelScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var cancelled = await bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoiceId), CancellationToken.None);
        cancelled.IsSuccess.Should().BeTrue();
        return new ItemOnInvoice(itemId, invoiceId);
    }

    /// <summary>
    /// Seeds billable items of one client straight into the database, one save each so every item has its own creation
    /// moment and the first given is the oldest.
    /// </summary>
    /// <param name="factory">The host to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="items">The description and type of each item, and the invoice it sits on, or <see langword="null"/> for an uninvoiced one.</param>
    /// <returns>The primary keys of the new items, in the order given.</returns>
    public static async Task<List<int>> SeedItemsAsync(
        IntegrationTestFactory factory, int clientId, IReadOnlyList<(string Description, BillableItemType Type, int? InvoiceId)> items)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var ids = new List<int>();
        foreach (var (description, type, invoiceId) in items)
        {
            var item = BillableItem.Create(clientId, description, 10m, "USD", type, type == BillableItemType.Recurring ? "monthly" : null);
            if (invoiceId is { } invoice)
            {
                item.MarkInvoiced(invoice);
            }

            context.BillableItems.Add(item);
            await context.SaveChangesAsync(CancellationToken.None);
            ids.Add(item.Id);
        }

        return ids;
    }

    /// <summary>
    /// Seeds <paramref name="count"/> uninvoiced one-time items of one client in a single save, more than a page holds.
    /// </summary>
    /// <param name="factory">The host to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="description">The description every item carries.</param>
    /// <param name="count">How many items to seed.</param>
    /// <returns>The primary keys of the new items, in the order created.</returns>
    public static async Task<List<int>> SeedManyItemsAsync(IntegrationTestFactory factory, int clientId, string description, int count)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var items = Enumerable.Range(0, count)
            .Select(_ => BillableItem.Create(clientId, description, 10m, "USD", BillableItemType.OneTime))
            .ToList();
        context.BillableItems.AddRange(items);
        await context.SaveChangesAsync(CancellationToken.None);
        return items.Select(i => i.Id).ToList();
    }

    /// <summary>Runs the daily billable-items invoicing.</summary>
    /// <param name="factory">The host to run it on.</param>
    /// <returns>A task that completes once the run is done.</returns>
    public static async Task RunInvoicingAsync(IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);
    }

    /// <summary>Reads a billable item as stored.</summary>
    /// <param name="factory">The host whose database holds the item.</param>
    /// <param name="itemId">The item's id.</param>
    /// <returns>The stored item.</returns>
    public static async Task<BillableItem> ReadItemAsync(IntegrationTestFactory factory, int itemId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.BillableItems.AsNoTracking().SingleAsync(x => x.Id == itemId, CancellationToken.None);
    }
}
