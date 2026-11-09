using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="CheckOverdueInvoicesCronHandler"/> against Postgres: which unpaid invoices its
/// <c>DueDate &lt; now</c> filter transitions to <see cref="InvoiceStatus.Overdue"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckOverdueInvoicesCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Back-dates an invoice's due date through the real "update options" endpoint. There is no
    /// direct-write shortcut for this, since the cron's own filter, not a seeded row, is what
    /// every case here proves.
    /// </summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="invoiceId">The invoice to back-date.</param>
    /// <param name="dueDate">The due date to write.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task BackDateAsync(HttpClient admin, int invoiceId, DateTimeOffset dueDate)
    {
        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow.AddDays(-30),
            dueDate,
            paymentMethod = (string?)null,
            taxRate = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unpaid invoice whose due date has already passed is marked overdue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnpaidInvoicePastDueDate_IsMarkedOverdue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 9.99m);
        await BackDateAsync(admin, invoiceId, DateTimeOffset.UtcNow.AddDays(-1));

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await readContext.Invoices.AsNoTracking()
            .FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        reloaded.Status.Should().Be(InvoiceStatus.Overdue);
    }

    /// <summary>An unpaid invoice not yet past its due date is left untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnpaidInvoiceNotYetDue_IsNotMarkedOverdue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 9.99m);

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await readContext.Invoices.AsNoTracking()
            .FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        reloaded.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A draft invoice past what would be its due date is never touched: only Unpaid counts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DraftInvoice_IsNeverMarkedOverdue()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 9.99m, isDraft: true);
        await BackDateAsync(admin, invoiceId, DateTimeOffset.UtcNow.AddDays(-1));

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await readContext.Invoices.AsNoTracking()
            .FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        reloaded.Status.Should().Be(InvoiceStatus.Draft);
    }

    /// <summary>
    /// One invoice whose overdue marking cannot be saved does not stop the run: another overdue
    /// invoice is still marked, and the failing one is left Unpaid for the next run.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneInvoiceThatCannotBeMarked_DoesNotStopTheOthers()
    {
        var now = DateTimeOffset.UtcNow;
        var poisonedId = 0;
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(now),
            tracker => tracker.Entries<Invoice>().Any(e => e.State == EntityState.Modified && e.Entity.Id == poisonedId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var failing = await SeedPastDueInvoiceAsync(context, clientId, now);
        var other = await SeedPastDueInvoiceAsync(context, clientId, now);
        poisonedId = failing.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);

        var failingAfter = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == failing.Id, CancellationToken.None);
        var otherAfter = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == other.Id, CancellationToken.None);
        failingAfter.Status.Should().Be(InvoiceStatus.Unpaid);
        otherAfter.Status.Should().Be(InvoiceStatus.Overdue);
    }

    /// <summary>Seeds an unpaid invoice that fell due two days before <paramref name="now"/>.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="now">The moment of the run.</param>
    /// <returns>The saved invoice.</returns>
    private static async Task<Invoice> SeedPastDueInvoiceAsync(IHostpanelDbContext context, int clientId, DateTimeOffset now)
    {
        var invoice = Invoice.Create(clientId, now.AddDays(-2), "USD", now.AddDays(-16));
        invoice.AddItem("Hosting", 10m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice;
    }
}
