using Innovayse.Hostpanel.Application.Billing.Commands.ProcessBillableItemsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ProcessBillableItemsCronHandler"/> against Postgres: which due billable items,
/// one-time and recurring, are invoiced and how a recurring item is advanced afterwards.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProcessBillableItemsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a client and one billable item due now, directly against the database.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="type">Whether the item is one-time or recurring.</param>
    /// <param name="nextDueDate">The item's recorded due date.</param>
    /// <returns>The client id and the seeded item.</returns>
    private static async Task<DueBillableItem> SeedDueItemAsync(
        IHostpanelDbContext context, BillableItemType type, DateTimeOffset? nextDueDate)
    {
        var client = Client.Create(
            $"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var item = BillableItem.Create(
            client.Id, "Consulting", 40m, "USD", type,
            recurringPeriod: type == BillableItemType.Recurring ? RecurringPeriodNames.Monthly : null, nextDueDate: nextDueDate);
        context.BillableItems.Add(item);
        await context.SaveChangesAsync(CancellationToken.None);

        return new(client.Id, item);
    }

    /// <summary>A one-time item due now is invoiced and marked invoiced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueOneTimeItem_IsInvoiced()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, item) = await SeedDueItemAsync(
            context, BillableItemType.OneTime, DateTimeOffset.UtcNow.AddDays(-1));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);

        var invoice = await context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ClientId == clientId, CancellationToken.None);
        invoice.Should().NotBeNull();
        invoice!.Items.Should().ContainSingle(i => i.Description == "Consulting" && i.UnitPrice == 40m);

        var reloaded = await context.BillableItems.AsNoTracking()
            .FirstAsync(x => x.Id == item.Id, CancellationToken.None);
        reloaded.IsInvoiced.Should().BeTrue();
        reloaded.InvoiceId.Should().Be(invoice.Id);
    }

    /// <summary>A one-time item not yet due is left uninvoiced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NotYetDueOneTimeItem_IsNotInvoiced()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, item) = await SeedDueItemAsync(
            context, BillableItemType.OneTime, DateTimeOffset.UtcNow.AddDays(5));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);

        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.ClientId == clientId, CancellationToken.None);
        invoice.Should().BeNull();

        var reloaded = await context.BillableItems.AsNoTracking()
            .FirstAsync(x => x.Id == item.Id, CancellationToken.None);
        reloaded.IsInvoiced.Should().BeFalse();
    }

    /// <summary>A recurring item due now is invoiced and re-armed: its due date advances and it is eligible again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueRecurringItem_IsInvoicedAndAdvanced()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var due = DateTimeOffset.UtcNow.AddDays(-1);
        var (clientId, item) = await SeedDueItemAsync(context, BillableItemType.Recurring, due);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);

        var invoice = await context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.ClientId == clientId, CancellationToken.None);
        invoice.Should().NotBeNull();
        invoice!.Items.Should().ContainSingle(i => i.Description == "Consulting" && i.UnitPrice == 40m);

        var reloaded = await context.BillableItems.AsNoTracking()
            .FirstAsync(x => x.Id == item.Id, CancellationToken.None);
        // AdvanceDueDate re-arms a recurring item for its next cycle, so it reads as not yet invoiced.
        reloaded.IsInvoiced.Should().BeFalse();
        reloaded.InvoiceId.Should().BeNull();
        reloaded.NextDueDate.Should().BeAfter(due);
    }

    /// <summary>
    /// Due is decided by the host clock: a one-time item due in five days is not invoiced today and
    /// is invoiced once the clock stands past its due date.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FutureOneTimeItem_IsInvoicedOnceTheClockReachesItsDueDate()
    {
        using var host = new ClockedHost(factory);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, item) = await SeedDueItemAsync(
            context, BillableItemType.OneTime, host.Clock.GetUtcNow().AddDays(5));
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);
        var invoicedToday = await context.Invoices.AnyAsync(i => i.ClientId == clientId, CancellationToken.None);

        host.Clock.Advance(TimeSpan.FromDays(6));
        await bus.InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);
        var reloaded = await context.BillableItems.AsNoTracking()
            .FirstAsync(x => x.Id == item.Id, CancellationToken.None);

        invoicedToday.Should().BeFalse();
        reloaded.IsInvoiced.Should().BeTrue();
    }

    /// <summary>
    /// One client whose invoicing fails midway does not stop the run and leaves nothing behind: the
    /// other client is invoiced, the failing one has neither an invoice nor an invoiced item, so the
    /// next run invoices it once rather than twice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneFailingClient_DoesNotStopTheOthersAndLeavesNoInvoice()
    {
        var now = DateTimeOffset.UtcNow;
        var poisonedItemId = 0;
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(now),
            tracker => tracker.Entries<BillableItem>().Any(e => e.State == EntityState.Modified && e.Entity.Id == poisonedItemId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (failingClientId, failingItem) = await SeedDueItemAsync(context, BillableItemType.OneTime, now.AddDays(-1));
        var (otherClientId, otherItem) = await SeedDueItemAsync(context, BillableItemType.OneTime, now.AddDays(-1));
        poisonedItemId = failingItem.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);

        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == failingClientId, CancellationToken.None))
            .Should().BeFalse();
        (await context.BillableItems.AsNoTracking().FirstAsync(x => x.Id == failingItem.Id, CancellationToken.None))
            .IsInvoiced.Should().BeFalse();
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == otherClientId, CancellationToken.None))
            .Should().Be(1);
        (await context.BillableItems.AsNoTracking().FirstAsync(x => x.Id == otherItem.Id, CancellationToken.None))
            .IsInvoiced.Should().BeTrue();

        poisonedItemId = 0;
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ProcessBillableItemsCronCommand(), CancellationToken.None);

        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == failingClientId, CancellationToken.None))
            .Should().Be(1);
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == otherClientId, CancellationToken.None))
            .Should().Be(1);
    }
}
