using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo seed is built from the clock of the reset, not from a calendar: after a reset on any day the dashboard has
/// payments on many days of the last ninety, customers and orders in the last weeks, a few overdue invoices among many
/// paid ones, and tickets and domains of every age; the amounts of rows do not depend on the day; and moving the clock
/// moves the whole picture with it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoSeedRelativeToClockTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The reset moment of the tests that read one seed: a mid-month afternoon.</summary>
    private static readonly DateTimeOffset Afternoon = new(2026, 11, 21, 15, 30, 0, TimeSpan.Zero);

    /// <summary>Midnight of the day <see cref="Afternoon"/> falls on, the anchor the seed is measured from.</summary>
    private static readonly DateTimeOffset Midnight = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Resets on a clock standing at a moment, so the seed is dated from that moment.</summary>
    /// <param name="now">The moment the clock stands at while the demo resets.</param>
    /// <returns>The clocked host, still open so the seeded rows can be read; the caller disposes it.</returns>
    private async Task<ClockedHost> ResetAtAsync(DateTimeOffset now)
    {
        var host = new ClockedHost(DemoTestHost.Enabled(factory), now);
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().ResetAsync(CancellationToken.None);
        return host;
    }

    /// <summary>Whole days between a moment and the clock's day, so a row is described by how old it is.</summary>
    /// <param name="moment">The moment of the row.</param>
    /// <param name="now">The reset moment.</param>
    /// <returns>Days ago, zero for today.</returns>
    private static int DaysAgo(DateTimeOffset moment, DateTimeOffset now) => (int)(now.UtcDateTime.Date - moment.UtcDateTime.Date).TotalDays;

    /// <summary>Payments arrive on many different days of the last thirty and ninety, and a recent period is not empty beside the one before it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Payments_AreSpreadOverTheLastNinetyDaysAsync()
    {
        using var host = await ResetAtAsync(Afternoon);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var payments = (await db.InvoiceTransactions.Where(t => t.Type == InvoiceTransactionType.Payment).Select(t => t.Date).ToListAsync())
            .Select(d => DaysAgo(d, Afternoon)).ToList();

        payments.Should().OnlyContain(d => d >= 0, "no payment is dated after the reset");
        var lastThirty = payments.Where(d => d < 30).ToList();
        var previousThirty = payments.Where(d => d is >= 30 and < 60).ToList();
        lastThirty.Distinct().Count().Should().BeGreaterThanOrEqualTo(15, "income shows on many days of the month, not on two");
        previousThirty.Distinct().Count().Should().BeGreaterThanOrEqualTo(15);
        lastThirty.Count.Should().BeGreaterThan(previousThirty.Count * 6 / 10, "the last month is not a collapse against the one before");
        payments.Where(d => d < 90).Distinct().Count().Should().BeGreaterThanOrEqualTo(60);
    }

    /// <summary>Most invoices are paid, the newest are open, and only a few are overdue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Invoices_AreMostlyPaidWithAFewOverdueAsync()
    {
        using var host = await ResetAtAsync(Afternoon);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var statuses = await db.Invoices.GroupBy(i => i.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var total = statuses.Values.Sum();

        statuses[InvoiceStatus.Paid].Should().BeGreaterThan(total * 6 / 10);
        statuses[InvoiceStatus.Unpaid].Should().BeGreaterThan(0);
        statuses[InvoiceStatus.Overdue].Should().BeInRange(1, total / 10, "a few invoices are overdue, not most of them");
        (await db.Invoices.Where(i => i.Status == InvoiceStatus.Overdue).AllAsync(i => i.DueDate < Afternoon))
            .Should().BeTrue("an invoice is overdue only after its due date");
        (await db.Invoices.Where(i => i.Status == InvoiceStatus.Unpaid).AllAsync(i => i.DueDate >= Midnight))
            .Should().BeTrue("an invoice still inside its payment term is not overdue");
    }

    /// <summary>New customers and orders arrive on several different days of the last weeks, and nothing is dated after the reset.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CustomersAndOrders_ArriveInTheLastWeeksAsync()
    {
        using var host = await ResetAtAsync(Afternoon);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signups = (await db.Clients.Select(c => c.CreatedAt).ToListAsync()).Select(d => DaysAgo(d, Afternoon)).ToList();
        var orders = (await db.Orders.Select(o => o.CreatedAt).ToListAsync()).Select(d => DaysAgo(d, Afternoon)).ToList();

        signups.Should().OnlyContain(d => d >= 0);
        signups.Where(d => d < 30).Distinct().Count().Should().BeGreaterThanOrEqualTo(8, "customers keep joining");
        signups.Count(d => d < 7).Should().BeGreaterThanOrEqualTo(3);
        orders.Should().OnlyContain(d => d >= 0);
        orders.Where(d => d < 30).Distinct().Count().Should().BeGreaterThanOrEqualTo(15, "orders arrive across the month");
        orders.Count(d => d < 90).Should().BeGreaterThan(orders.Count / 2, "most orders are recent");
        orders.Count(d => d >= 90).Should().BeGreaterThan(0, "the yearly report keeps an older tail");
    }

    /// <summary>Tickets have many different ages and domains expire at many different times, before and after the reset.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TicketsAndDomains_HaveVariedAgesAsync()
    {
        using var host = await ResetAtAsync(Afternoon);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tickets = (await db.Tickets.Select(t => t.CreatedAt).ToListAsync()).Select(d => DaysAgo(d, Afternoon)).ToList();
        var expiries = (await db.Domains.Where(d => d.Status == DomainStatus.Active).Select(d => d.ExpiresAt).ToListAsync())
            .Select(d => -DaysAgo(d, Afternoon)).ToList();

        tickets.Distinct().Count().Should().BeGreaterThanOrEqualTo(30);
        tickets.Min().Should().BeLessThan(5, "there are fresh tickets");
        tickets.Max().Should().BeGreaterThan(45, "and old ones");
        expiries.Should().OnlyContain(days => days > 0, "an active domain has not expired");
        expiries.Count(days => days <= 30).Should().BeGreaterThanOrEqualTo(2, "some domains expire within the month");
        expiries.Count(days => days > 180).Should().BeGreaterThanOrEqualTo(2, "and some in more than half a year");
        expiries.Distinct().Count().Should().BeGreaterThanOrEqualTo(8);
    }

    /// <summary>
    /// A reset on another day, across a month end and in another year, produces the same amounts of rows and the same
    /// picture shifted by the days between the resets: the customers of the last weeks joined the same number of days ago.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Seed_MovesWithTheClockAndKeepsItsAmountsAsync()
    {
        var later = new DateTimeOffset(2027, 3, 1, 2, 10, 0, TimeSpan.Zero);

        int invoices;
        List<int> signups;
        using (var host = await ResetAtAsync(Afternoon))
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            invoices = await db.Invoices.CountAsync();
            signups = (await db.Clients.Select(c => c.CreatedAt).ToListAsync()).Select(d => DaysAgo(d, Afternoon)).Where(d => d < 90).Order().ToList();
        }

        using var laterHost = await ResetAtAsync(later);
        using var laterScope = laterHost.Services.CreateScope();
        var laterDb = laterScope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await laterDb.Invoices.CountAsync()).Should().Be(invoices, "the amount of rows does not depend on the reset day");
        (await laterDb.Clients.Select(c => c.CreatedAt).ToListAsync()).Select(d => DaysAgo(d, later)).Where(d => d < 90).Order()
            .Should().Equal(signups, "the customers of the last weeks joined the same number of days before the reset");
        (await laterDb.Clients.AnyAsync(c => c.CreatedAt > later)).Should().BeFalse("nothing is dated after the reset");
        (await laterDb.InvoiceTransactions.AnyAsync(t => t.Date > later)).Should().BeFalse();
        (await laterDb.Orders.AnyAsync(o => o.CreatedAt > later)).Should().BeFalse();
    }
}
