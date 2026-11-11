using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo seed reads as a believable history, not as rows stamped with the moment of seeding: every date is in
/// the past or the planned future it should be, a customer's records are never older than the customer, a
/// quote is never written after it expired, a resolved incident lasts minutes or hours, a server never holds more
/// accounts than its limit, tickets sit in the department that handles their subject, and the storefront slides
/// point at pages that exist.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoSeedConsistencyTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The storefront pages a slide may link to; anything else is a dead link.</summary>
    private static readonly string[] SlideTargets = ["/hosting", "/domains", "/products"];

    /// <summary>Wipes the database and seeds it from scratch, then hands out a scope to read it through.</summary>
    /// <returns>The scope; the caller disposes it.</returns>
    private async Task<IServiceScope> SeededScopeAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IDemoDataManager>().ResetAsync(CancellationToken.None);
        }

        return host.Services.CreateScope();
    }

    /// <summary>Every slide links to a storefront page that exists, not to a path with no page behind it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Slides_LinkOnlyToPagesThatExistAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var links = await db.Slides.Select(s => s.LearnMoreUrl).ToListAsync();

        links.Should().NotBeEmpty().And.OnlyContain(l => l != null && SlideTargets.Any(t => l == t || l.StartsWith(t + "/", StringComparison.Ordinal)));
    }

    /// <summary>Tickets are opened on different days in the past, and each reply follows the message before it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Tickets_AreSpreadOverThePastAndRepliesFollowEachOtherAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var tickets = await db.Tickets.Include(t => t.Replies).ToListAsync();

        tickets.Select(t => t.CreatedAt.Date).Distinct().Count().Should().BeGreaterThan(30, "tickets were opened over weeks, not at once");
        tickets.Should().OnlyContain(t => t.CreatedAt < now);
        foreach (var ticket in tickets)
        {
            var moments = ticket.Replies.OrderBy(r => r.Id).Select(r => r.CreatedAt).Prepend(ticket.CreatedAt).ToList();
            moments.Should().BeInAscendingOrder().And.OnlyContain(m => m <= now);
        }
    }

    /// <summary>A ticket about abuse or security goes to that department, one about an invoice to billing, and customers sign their replies with their own name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Tickets_SitInTheDepartmentThatHandlesTheirSubjectAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var departments = await db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name);

        var tickets = await db.Tickets.Include(t => t.Replies).ToListAsync();

        tickets.Where(t => t.Subject.StartsWith("Abuse report", StringComparison.Ordinal))
            .Should().NotBeEmpty().And.OnlyContain(t => departments[t.DepartmentId!.Value] == "Abuse and Security");
        tickets.Where(t => t.Subject.StartsWith("Question about the invoice", StringComparison.Ordinal))
            .Should().NotBeEmpty().And.OnlyContain(t => departments[t.DepartmentId!.Value] == "Billing");
        tickets.Where(t => t.Subject.StartsWith("Add a staging copy", StringComparison.Ordinal))
            .Should().NotBeEmpty().And.OnlyContain(t => departments[t.DepartmentId!.Value] == "Technical Support");
        tickets.SelectMany(t => t.Replies).Where(r => !r.IsStaffReply).Should().NotBeEmpty()
            .And.OnlyContain(r => r.AuthorName != "Customer" && r.AuthorName.Contains(' ', StringComparison.Ordinal));
    }

    /// <summary>Announcements are posted on different days, none in the future.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Announcements_AreSpreadOverThePastAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dates = await db.Announcements.Select(a => a.CreatedAt).ToListAsync();

        dates.Select(d => d.Date).Distinct().Should().HaveCount(dates.Count);
        dates.Should().OnlyContain(d => d < DateTimeOffset.UtcNow);
    }

    /// <summary>A resolved incident ends after it started, within a day, and nothing ends in the future.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolvedNetworkIssues_LastMinutesOrHoursAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var resolved = await db.NetworkIssues.Where(n => n.Status == NetworkIssueStatus.Resolved).ToListAsync();

        resolved.Should().NotBeEmpty().And.OnlyContain(n =>
            n.EndDate != null && n.EndDate > n.StartDate && n.EndDate - n.StartDate < TimeSpan.FromDays(1) && n.EndDate < DateTimeOffset.UtcNow);
    }

    /// <summary>A server never holds more accounts than the limit it is set up with.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Servers_NeverHoldMoreAccountsThanTheirLimitAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var servers = await db.Servers.Where(s => s.AccountsCount != null).ToListAsync();

        servers.Should().NotBeEmpty().And.OnlyContain(s => s.AccountsCount <= s.MaxAccounts);
    }

    /// <summary>Customers signed up on different days, and nothing in a customer's history is older than the customer.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CustomerRecords_AreNeverOlderThanTheCustomerAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signups = await db.Clients.ToDictionaryAsync(c => c.Id, c => c.CreatedAt);

        signups.Values.Select(v => v.Date).Distinct().Count().Should().BeGreaterThan(5, "customers did not all sign up on one day");
        signups.Values.Should().OnlyContain(v => v < DateTimeOffset.UtcNow);
        (await db.ClientServices.Select(s => new { s.ClientId, s.CreatedAt }).ToListAsync())
            .Should().OnlyContain(s => s.CreatedAt >= signups[s.ClientId]);
        (await db.Orders.Select(o => new { o.ClientId, o.CreatedAt }).ToListAsync())
            .Should().OnlyContain(o => o.CreatedAt >= signups[o.ClientId]);
        (await db.Invoices.Select(i => new { i.ClientId, i.InvoiceDate }).ToListAsync())
            .Should().OnlyContain(i => i.InvoiceDate >= signups[i.ClientId]);
        (await db.Domains.Where(d => d.Status != DomainStatus.PendingRegistration && d.Status != DomainStatus.PendingTransfer)
                .Select(d => new { d.ClientId, d.RegisteredAt }).ToListAsync())
            .Should().OnlyContain(d => d.RegisteredAt >= signups[d.ClientId]);
        (await db.Quotes.Select(q => new { q.ClientId, q.CreatedAt }).ToListAsync())
            .Should().OnlyContain(q => q.CreatedAt >= signups[q.ClientId]);
    }

    /// <summary>A domain is registered in the past, before it expires; a quote is written before it expires, and only an expired one has already expired.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainsAndQuotes_AreDatedInTheirOwnOrderAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        (await db.Domains.Where(d => d.Status == DomainStatus.Active).Select(d => new { d.RegisteredAt, d.ExpiresAt }).ToListAsync())
            .Should().NotBeEmpty().And.OnlyContain(d => d.RegisteredAt < d.ExpiresAt && d.RegisteredAt < now);

        var quotes = await db.Quotes.Select(q => new { q.CreatedAt, q.ExpiryDate, q.Stage }).ToListAsync();
        quotes.Should().OnlyContain(q => q.CreatedAt < q.ExpiryDate && q.CreatedAt < now);
        quotes.Where(q => q.Stage == QuoteStage.Expired).Should().NotBeEmpty().And.OnlyContain(q => q.ExpiryDate < now);
        quotes.Where(q => q.Stage != QuoteStage.Expired).Should().OnlyContain(q => q.ExpiryDate > now);
    }

    /// <summary>The logs read as history: mail and activity are dated in the past on many different days.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Logs_AreSpreadOverThePastAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var sent = await db.EmailLogs.Select(e => e.SentAt).ToListAsync();
        var performed = await db.ActivityLogs.Select(a => a.CreatedAt).ToListAsync();
        var requested = await db.CancellationRequests.Select(c => c.CreatedAt).ToListAsync();
        var published = await db.Downloads.Select(d => d.CreatedAt).ToListAsync();

        foreach (var moments in new[] { sent, performed, requested, published })
        {
            moments.Select(m => m.Date).Distinct().Count().Should().BeGreaterThan(10);
            moments.Should().OnlyContain(m => m < now);
        }
    }

    /// <summary>The welcome mail template names the demo site instead of the product's own name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WelcomeTemplate_NamesTheDemoAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var welcome = await db.EmailTemplates.SingleAsync(t => t.Slug == "welcome" && t.Language == "en");

        welcome.Subject.Should().Contain(DemoPortalSettingsSeeder.SiteName);
    }

    /// <summary>
    /// A visitor can provision any pending service the demo shows: every hosting plan names its panel package, and
    /// every pending service is a hosting service that already carries the domain and username its account is created with.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingServices_CarryWhatProvisioningNeedsAsync()
    {
        using var scope = await SeededScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hostingPlans = await db.Products
            .Where(p => p.Type == ProductType.SharedHosting || p.Type == ProductType.Vps || p.Type == ProductType.Dedicated)
            .ToListAsync();
        var pending = await db.ClientServices.Where(s => s.Status == ServiceStatus.Pending).ToListAsync();
        var productTypes = await db.Products.ToDictionaryAsync(p => p.Id, p => p.Type);

        hostingPlans.Should().NotBeEmpty().And.OnlyContain(p => !string.IsNullOrWhiteSpace(p.PackageName));
        pending.Should().NotBeEmpty();
        pending.Should().OnlyContain(s =>
            DemoCatalogueSeeder.IsHosting(productTypes[s.ProductId])
            && !string.IsNullOrWhiteSpace(s.Domain)
            && !string.IsNullOrWhiteSpace(s.Username));
    }
}
