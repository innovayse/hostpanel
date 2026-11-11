using Innovayse.Hostpanel.Application.Billing.Commands.InvoiceClientBillableItems;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="InvoiceClientBillableItemsHandler"/> against Postgres, on a fixed clock: the due items
/// of one client are invoiced together, and a failure between the invoice and the marking of its
/// items leaves neither behind, so the next run cannot invoice the same items twice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoiceClientBillableItemsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment every test runs at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 6, 0, 0, TimeSpan.Zero);

    /// <summary>Seeds a client and two billable items of the given type, both due a day before <see cref="Now"/>.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="type">Whether the items are one-time or recurring.</param>
    /// <returns>The client id and the two items.</returns>
    private static async Task<SeededBillableItems> SeedAsync(IHostpanelDbContext context, BillableItemType type)
    {
        var client = Client.Create(
            $"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", Now);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var first = NewItem(client.Id, "Consulting", type);
        var second = NewItem(client.Id, "Support", type);
        context.BillableItems.AddRange(first, second);
        await context.SaveChangesAsync(CancellationToken.None);
        return new(client.Id, first, second);
    }

    /// <summary>Builds one billable item due a day before <see cref="Now"/>.</summary>
    /// <param name="clientId">The billed client.</param>
    /// <param name="description">The line description.</param>
    /// <param name="type">Whether the item is one-time or recurring.</param>
    /// <returns>The unsaved item.</returns>
    private static BillableItem NewItem(int clientId, string description, BillableItemType type) =>
        BillableItem.Create(
            clientId, description, 40m, "USD", type,
            recurringPeriod: type == BillableItemType.Recurring ? RecurringPeriodNames.Monthly : null,
            nextDueDate: Now.AddDays(-1));

    /// <summary>Tells whether a pending change set modifies the given item (the second save of a one-time invoice).</summary>
    /// <param name="tracker">The change tracker about to be saved.</param>
    /// <param name="itemId">The item whose marking is refused.</param>
    /// <returns><see langword="true"/> when that item is modified in the pending set.</returns>
    private static bool MarksItem(ChangeTracker tracker, int itemId) =>
        tracker.Entries<BillableItem>().Any(e => e.State == EntityState.Modified && e.Entity.Id == itemId);

    /// <summary>All of a client's one-time items due now land on one invoice and are marked with its id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueOneTimeItems_AreInvoicedTogetherAndMarked()
    {
        using var host = new ClockedHost(factory, Now);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAsync(context, BillableItemType.OneTime);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new InvoiceClientBillableItemsCommand(seeded.ClientId, BillableItemType.OneTime), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var invoice = await context.Invoices.Include(i => i.Items).AsNoTracking()
            .SingleAsync(i => i.ClientId == seeded.ClientId, CancellationToken.None);
        invoice.Items.Should().HaveCount(2);
        var items = await context.BillableItems.AsNoTracking()
            .Where(x => x.Id == seeded.First.Id || x.Id == seeded.Second.Id).ToListAsync(CancellationToken.None);
        items.Should().OnlyContain(x => x.IsInvoiced && x.InvoiceId == invoice.Id);
    }

    /// <summary>Recurring items due now are invoiced and re-armed in the same save, with no invoice id kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueRecurringItems_AreInvoicedAndReArmed()
    {
        using var host = new ClockedHost(factory, Now);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAsync(context, BillableItemType.Recurring);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync<Result>(
            new InvoiceClientBillableItemsCommand(seeded.ClientId, BillableItemType.Recurring), CancellationToken.None);

        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == seeded.ClientId, CancellationToken.None))
            .Should().Be(1);
        var reloaded = await context.BillableItems.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.First.Id, CancellationToken.None);
        reloaded.IsInvoiced.Should().BeFalse();
        reloaded.NextDueDate.Should().Be(Now.AddDays(-1).AddMonths(1));
    }

    /// <summary>Items not yet due are left alone and no invoice is raised.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NothingDue_RaisesNoInvoice()
    {
        using var host = new ClockedHost(factory, Now.AddDays(-30));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAsync(context, BillableItemType.OneTime);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new InvoiceClientBillableItemsCommand(seeded.ClientId, BillableItemType.OneTime), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == seeded.ClientId, CancellationToken.None))
            .Should().BeFalse();
    }

    /// <summary>
    /// A failure on the save that marks the items leaves no invoice behind (the transaction rolls
    /// back), so the next run invoices the items once, not twice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CrashBetweenTheInvoiceAndTheMarking_CannotDoubleInvoice()
    {
        var poisonedItemId = 0;
        using var host = FailingSaveHost.Create(
            factory, new ManualTimeProvider(Now), tracker => MarksItem(tracker, poisonedItemId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var seeded = await SeedAsync(context, BillableItemType.OneTime);
        poisonedItemId = seeded.First.Id;
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var command = new InvoiceClientBillableItemsCommand(seeded.ClientId, BillableItemType.OneTime);

        var crashed = async () => await bus.InvokeAsync<Result>(command, CancellationToken.None);
        await crashed.Should().ThrowAsync<InvalidOperationException>();

        using var rereadScope = host.Services.CreateScope();
        var reread = rereadScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await reread.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == seeded.ClientId, CancellationToken.None))
            .Should().BeFalse();
        (await reread.BillableItems.AsNoTracking().SingleAsync(x => x.Id == seeded.First.Id, CancellationToken.None))
            .IsInvoiced.Should().BeFalse();

        poisonedItemId = 0;
        using var retryScope = host.Services.CreateScope();
        await retryScope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result>(command, CancellationToken.None);

        (await reread.Invoices.AsNoTracking().CountAsync(i => i.ClientId == seeded.ClientId, CancellationToken.None))
            .Should().Be(1);
    }
}
