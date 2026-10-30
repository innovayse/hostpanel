using Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The lapse sweep of <see cref="CheckOverdueInvoicesCronHandler"/> against Postgres: an overdue invoice
/// lapses once every digital renewal line on it is past the grace period its own snapshot kept, a run repeated
/// changes nothing, and a hosting line is never touched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LapseOverdueInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The grace period the digital lines in these tests are sold with, in days.</summary>
    private const int Grace = 7;

    /// <summary>
    /// The grace boundary through the whole cron: a digital renewal invoice is not lapsed at the end of its
    /// grace period, and is a moment after it.
    /// </summary>
    /// <param name="secondsAfterGraceEnd">How far past the end of the grace period the run is.</param>
    /// <param name="lapsed">Whether the invoice must be lapsed afterwards.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task GraceBoundary_DecidesWhetherTheInvoiceLapses(int secondsAfterGraceEnd, bool lapsed)
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-Grace).AddSeconds(-secondsAfterGraceEnd);
        var invoiceId = await SeedAsync(host, due, i => AddDigitalRenewal(i, 1, due, Grace));

        await RunCronAsync(host);

        var invoice = await LoadAsync(host, invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        (invoice.LapsedAt is not null).Should().Be(lapsed);
    }

    /// <summary>A second run reports nothing new: the moment the invoice lapsed at is kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RepeatedRuns_LapseTheInvoiceOnce()
    {
        var now = WholeMicroseconds();
        var clock = new ManualTimeProvider(now);
        using var host = FailingSaveHost.Create(factory, clock, _ => false);
        var due = now.AddDays(-Grace - 1);
        var invoiceId = await SeedAsync(host, due, i => AddDigitalRenewal(i, 1, due, Grace));

        await RunCronAsync(host);
        var first = (await LoadAsync(host, invoiceId)).LapsedAt;
        clock.Advance(TimeSpan.FromDays(2));
        await RunCronAsync(host);

        first.Should().Be(now);
        (await LoadAsync(host, invoiceId)).LapsedAt.Should().Be(first);
    }

    /// <summary>
    /// A mixed invoice, a hosting line and a digital renewal line, lapses on the digital line alone, and the
    /// hosting line is left exactly as it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MixedInvoice_LapsesOnItsDigitalLineAndLeavesTheHostingLineAlone()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-Grace - 1);
        var invoiceId = await SeedAsync(host, due, i =>
        {
            i.AddServiceRenewal("Hosting", 5m, 11, due, due.AddMonths(1), Snapshot(digital: false, days: 60));
            AddDigitalRenewal(i, 12, due, Grace);
        });
        var before = await LoadAsync(host, invoiceId);

        await RunCronAsync(host);

        var after = await LoadAsync(host, invoiceId);
        after.LapsedAt.Should().NotBeNull();
        after.Items.Should().BeEquivalentTo(before.Items);
        after.Total.Should().Be(before.Total);
    }

    /// <summary>A mixed invoice whose digital line is still within its grace period does not lapse, however old the hosting line is.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task MixedInvoice_WithDigitalLineWithinGrace_DoesNotLapse()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-3);
        var invoiceId = await SeedAsync(host, due, i =>
        {
            i.AddServiceRenewal("Hosting", 5m, 11, due, due.AddMonths(1), Snapshot(digital: false, days: 0));
            AddDigitalRenewal(i, 12, due, Grace);
        });

        await RunCronAsync(host);

        (await LoadAsync(host, invoiceId)).LapsedAt.Should().BeNull();
    }

    /// <summary>With two digital renewal lines the invoice lapses only after the longer grace period.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task TwoDigitalLines_LapseOnlyWhenBothArePastGrace()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-5);
        var invoiceId = await SeedAsync(host, due, i =>
        {
            AddDigitalRenewal(i, 1, due, 3);
            AddDigitalRenewal(i, 2, due, 10);
        });

        await RunCronAsync(host);

        (await LoadAsync(host, invoiceId)).LapsedAt.Should().BeNull();
    }

    /// <summary>The grace period comes from the snapshot on the line: a line sold with no grace lapses at once, whatever the catalogue says now.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task GraceComesFromTheLineSnapshot()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-1);
        var invoiceId = await SeedAsync(host, due, i => AddDigitalRenewal(i, 1, due, 0));

        await RunCronAsync(host);

        (await LoadAsync(host, invoiceId)).LapsedAt.Should().Be(now);
    }

    /// <summary>Invoices that are not digital renewals never lapse: hosting only, a one-off digital line, and a snapshot without a grace period.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task InvoicesWithoutADigitalRenewalLine_NeverLapse()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-400);
        var hosting = await SeedAsync(host, due, i =>
            i.AddServiceRenewal("Hosting", 5m, 11, due, due.AddMonths(1), Snapshot(digital: false, days: 0)));
        var oneOff = await SeedAsync(host, due, i => i.AddItem("Licence", 5m, 1, null, Snapshot(digital: true, days: 0)));
        var unknownGrace = await SeedAsync(host, due, i =>
            i.AddServiceRenewal("Licence", 5m, 13, due, due.AddMonths(1), Snapshot(digital: true, days: null)));

        await RunCronAsync(host);

        foreach (var id in new[] { hosting, oneOff, unknownGrace })
        {
            var invoice = await LoadAsync(host, id);
            invoice.Status.Should().Be(InvoiceStatus.Overdue);
            invoice.LapsedAt.Should().BeNull();
        }
    }

    /// <summary>An invoice paid before the run is no longer overdue, so it is not lapsed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PaidInvoice_IsNotLapsed()
    {
        var now = WholeMicroseconds();
        using var host = Host(now);
        var due = now.AddDays(-Grace - 1);
        var invoiceId = await SeedAsync(host, due, i => AddDigitalRenewal(i, 1, due, Grace));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.Include(i => i.Items).Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            invoice.Settle("manual", "lapse-paid", now);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await RunCronAsync(host);

        (await LoadAsync(host, invoiceId)).LapsedAt.Should().BeNull();
    }

    /// <summary>The current moment cut to whole microseconds, so a date written to Postgres reads back equal.</summary>
    /// <returns>The moment.</returns>
    private static DateTimeOffset WholeMicroseconds()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
    }

    /// <summary>Builds a host whose clock reads <paramref name="now"/>.</summary>
    /// <param name="now">The moment the cron runs at.</param>
    /// <returns>The host; the caller disposes it.</returns>
    private WebApplicationFactory<Program> Host(DateTimeOffset now) =>
        FailingSaveHost.Create(factory, new ManualTimeProvider(now), _ => false);

    /// <summary>Builds the snapshot of a product sold on a monthly cycle.</summary>
    /// <param name="digital">Whether the product is digital.</param>
    /// <param name="days">The grace period, or <see langword="null"/> for none recorded.</param>
    /// <returns>The snapshot.</returns>
    private static ProductSnapshot Snapshot(bool digital, int? days) =>
        new(9, BillingCycle.Monthly, digital, null, null, days);

    /// <summary>Adds a digital renewal line sold with the given grace period.</summary>
    /// <param name="invoice">The invoice to add to.</param>
    /// <param name="serviceId">The renewed service.</param>
    /// <param name="due">The invoice's due date, the start of the renewed period.</param>
    /// <param name="days">The grace period recorded on the line.</param>
    private static void AddDigitalRenewal(Invoice invoice, int serviceId, DateTimeOffset due, int days)
    {
        invoice.AddServiceRenewal("Licence", 10m, serviceId, due, due.AddMonths(1), Snapshot(digital: true, days: days));
    }

    /// <summary>Saves a client and an unpaid invoice due at <paramref name="due"/> with the lines <paramref name="addLines"/> adds.</summary>
    /// <param name="host">The host to save through.</param>
    /// <param name="due">The invoice's due date.</param>
    /// <param name="addLines">Adds the invoice's lines.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> SeedAsync(WebApplicationFactory<Program> host, DateTimeOffset due, Action<Invoice> addLines)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create(
            $"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, due, "EUR", due.AddDays(-7));
        addLines(invoice);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Runs the overdue invoice cron once through the bus.</summary>
    /// <param name="host">The host to run it in.</param>
    /// <returns>A task.</returns>
    private static async Task RunCronAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CheckOverdueInvoicesCronCommand(), CancellationToken.None);
    }

    /// <summary>Loads an invoice with its lines as the database holds it.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice, untracked.</returns>
    private static async Task<Invoice> LoadAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices
            .AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);
    }
}
