using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo seeder through <see cref="IDemoDataManager"/>: it fills a database with the sample data in exactly
/// the same amounts every time, a second run changes nothing, a reset wipes visitors' work and reseeds everything,
/// the sample covers every state the entities can reach, the published accounts really sign in, and a deployment
/// that is not a demo is left empty.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoDataManagerTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The amounts the seed produces; they are fixed, so a change here is a deliberate change of the demo.</summary>
    private static readonly Counts Expected = new(
        Currencies: 2, ProductGroups: 8, Products: 32, Prices: 128, Features: 164, Tlds: 19,
        Clients: 65, Users: 73, SubjectRoles: 73, Contacts: 99, ClientUsers: 3, Invitations: 4,
        Servers: 7, ServerGroups: 3, SslChecks: 66, DiskUsage: 58, Services: 140, Cancellations: 75,
        Domains: 108, DomainReminders: 40, DnsRecords: 280, ForwardingRules: 46,
        Invoices: 517, InvoiceTransactions: 399, Transactions: 409, Quotes: 66, QuoteItems: 165, BillableItems: 66, Orders: 160,
        Departments: 5, ReplyCategories: 4, Replies: 12, KbCategories: 7, Articles: 33, Tickets: 66, TicketReplies: 117,
        TicketTags: 88, Announcements: 25, NetworkIssues: 12,
        Slides: 7, SlideTranslations: 21, DownloadCategories: 4, Downloads: 21, ActivityLogs: 160, EmailLogs: 120,
        Testimonials: 12, TestimonialTranslations: 36, Partners: 16, PartnerTranslations: 48,
        ServerLocations: 6, ServerLocationTranslations: 18, Projects: 12, ProjectTranslations: 36, Faqs: 24, FaqTranslations: 72);

    /// <summary>Counts every seeded dataset.</summary>
    /// <param name="provider">The scope to read through.</param>
    /// <returns>The counts.</returns>
    private static async Task<Counts> CountAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AppDbContext>();
        return new Counts(
            await db.Currencies.CountAsync(), await db.ProductGroups.CountAsync(), await db.Products.CountAsync(),
            await db.ProductPrices.CountAsync(), await db.ProductFeatures.CountAsync(), await db.TldConfigs.CountAsync(),
            await db.Clients.CountAsync(), await provider.GetRequiredService<UserManager<AppUser>>().Users.CountAsync(),
            await db.SubjectRoles.CountAsync(), await db.Contacts.CountAsync(), await db.ClientUsers.CountAsync(),
            await db.Invitations.CountAsync(),
            await db.Servers.CountAsync(), await db.ServerGroups.CountAsync(), await db.SslChecks.CountAsync(),
            await db.DiskUsageStats.CountAsync(), await db.ClientServices.CountAsync(), await db.CancellationRequests.CountAsync(),
            await db.Domains.CountAsync(), await db.DomainReminders.CountAsync(), await db.Set<DnsRecord>().CountAsync(),
            await db.Set<EmailForwardingRule>().CountAsync(),
            await db.Invoices.CountAsync(), await db.InvoiceTransactions.CountAsync(), await db.Transactions.CountAsync(),
            await db.Quotes.CountAsync(), await db.QuoteItems.CountAsync(), await db.BillableItems.CountAsync(),
            await db.Orders.CountAsync(),
            await db.Departments.CountAsync(), await db.PredefinedReplyCategories.CountAsync(), await db.PredefinedReplies.CountAsync(),
            await db.KbCategories.CountAsync(), await db.KbArticles.CountAsync(), await db.Tickets.CountAsync(),
            await db.Set<TicketReply>().CountAsync(), await db.Set<TicketTag>().CountAsync(), await db.Announcements.CountAsync(),
            await db.NetworkIssues.CountAsync(),
            await db.Slides.CountAsync(), await db.SlideTranslations.CountAsync(), await db.DownloadCategories.CountAsync(),
            await db.Downloads.CountAsync(), await db.ActivityLogs.CountAsync(), await db.EmailLogs.CountAsync(),
            await db.Testimonials.CountAsync(), await db.TestimonialTranslations.CountAsync(), await db.Partners.CountAsync(),
            await db.PartnerTranslations.CountAsync(), await db.ServerLocations.CountAsync(),
            await db.ServerLocationTranslations.CountAsync(), await db.Projects.CountAsync(), await db.ProjectTranslations.CountAsync(),
            await db.Faqs.CountAsync(), await db.FaqTranslations.CountAsync());
    }

    /// <summary>Wipes the database and seeds it from scratch, so the assertions never depend on what an earlier test left.</summary>
    /// <param name="host">The demo host.</param>
    /// <returns>A task that completes once the sample is seeded.</returns>
    private static async Task ResetAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().ResetAsync(CancellationToken.None);
    }

    /// <summary>A clean seed produces exactly the documented amounts in every table.</summary>
    [Fact]
    public async Task Seed_ProducesTheDeterministicCountsAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);

        using var scope = host.Services.CreateScope();
        (await CountAsync(scope.ServiceProvider)).Should().Be(Expected);
    }

    /// <summary>Two resets in a row produce the identical amounts, so the demo looks the same every day.</summary>
    [Fact]
    public async Task Reset_ReseedsEverythingIdenticallyAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);

        // A visitor's leftovers: a client the sample does not contain. Its creation event is
        // held back, or the welcome email it triggers lands in the email log after the reset
        // has run and skews the count below.
        using (var scope = host.Services.CreateScope())
        {
            using var suppression = scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stray = Client.Create($"stray-{Guid.NewGuid():N}", "Stray", "Visitor", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
            db.Clients.Add(stray);
            await db.SaveChangesAsync();
        }

        await ResetAsync(host);

        using var after = host.Services.CreateScope();
        (await CountAsync(after.ServiceProvider)).Should().Be(Expected);
        (await after.ServiceProvider.GetRequiredService<AppDbContext>().Clients.AnyAsync(c => c.FirstName == "Stray"))
            .Should().BeFalse("a reset removes what visitors added");
    }

    /// <summary>Running the seed twice leaves exactly the rows the first run made.</summary>
    [Fact]
    public async Task SeedingTwice_ChangesNothingTheSecondTimeAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);

        using (var second = host.Services.CreateScope())
        {
            await second.ServiceProvider.GetRequiredService<IDemoDataManager>().SeedAsync(CancellationToken.None);
        }

        using var after = host.Services.CreateScope();
        (await CountAsync(after.ServiceProvider)).Should().Be(Expected);
    }

    /// <summary>Every plan of a product group carries the same feature labels in the same order, so no comparison cell is empty.</summary>
    [Fact]
    public async Task SeededPlans_ShareTheSameFeatureLabelsWithinAGroupAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.Products.ToListAsync();
        var features = (await db.ProductFeatures.ToListAsync()).ToLookup(f => f.ProductId);
        var byGroup = products.GroupBy(p => p.GroupId).ToList();

        byGroup.Should().NotBeEmpty();
        foreach (var group in byGroup)
        {
            var labelSets = group
                .Select(p => string.Join('|', features[p.Id].OrderBy(f => f.SortOrder).Select(f => f.Label)))
                .Distinct()
                .ToList();
            labelSets.Should().ContainSingle($"group {group.Key} must give every plan the same labels");
            group.Should().OnlyContain(p => features[p.Id].All(f => !string.IsNullOrWhiteSpace(f.Value)));
        }

        // The hosting plans are the ones the home page compares, so they carry a full specification.
        products.Where(p => p.Type == ProductType.SharedHosting).Should().OnlyContain(p => features[p.Id].Count() == 9);
    }

    /// <summary>The sample has one digital product with a SKU, small JSON metadata and a grace period, so the product form's new fields have data to show.</summary>
    [Fact]
    public async Task SeededCatalogue_HasOneDigitalProductWithSkuMetadataAndGracePeriodAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var digital = await db.Products.Where(p => p.Type == ProductType.Digital).ToListAsync();

        var product = digital.Should().ContainSingle().Subject;
        product.Sku.Should().Be(DemoCatalogueSeeder.DigitalSku);
        product.Metadata.Should().Be(DemoCatalogueSeeder.DigitalMetadata);
        product.GracePeriodDays.Should().Be(DemoCatalogueSeeder.DigitalGracePeriodDays);
        product.PackageName.Should().BeNull("a digital product is delivered, not provisioned on a hosting account");
    }

    /// <summary>Exactly one hosting plan and one VPS plan are featured, and each group's plans read in a logical order.</summary>
    [Fact]
    public async Task SeededPlans_FeatureTheMiddleTierAndOrderEachGroupAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.Products.ToListAsync();

        var featured = products.Where(p => p.IsFeatured).ToList();
        featured.Select(p => p.Type).Should().BeEquivalentTo([ProductType.SharedHosting, ProductType.Vps]);
        featured.Select(p => p.Slug).Should().BeEquivalentTo(["managed-site-hosting", "vps-pro"]);

        foreach (var group in products.GroupBy(p => p.GroupId))
        {
            group.Select(p => p.SortOrder).OrderBy(o => o).Should().Equal(Enumerable.Range(1, group.Count()));
        }

        products.Where(p => p.Type == ProductType.SharedHosting).OrderBy(p => p.SortOrder).Select(p => p.Slug)
            .Should().Equal("starter-hosting", "business-hosting", "managed-site-hosting", "pro-hosting", "reseller-hosting");
    }

    /// <summary>The sample covers every state the entities can reach, in both currencies, over twelve months.</summary>
    [Fact]
    public async Task SeededData_CoversEveryStateAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var currencies = await db.Currencies.ToListAsync();
        currencies.Should().ContainSingle(c => c.Code == "USD" && c.IsBase);
        currencies.Should().ContainSingle(c => c.Code == "EUR" && !c.IsBase && c.RateToBase > 0);

        var products = await db.Products.Include(p => p.Prices).Where(p => p.Type != ProductType.Domain).ToListAsync();
        products.Should().NotBeEmpty().And.OnlyContain(p =>
            p.Prices.Any(x => x.CurrencyCode == "USD" && x.Cycle == BillingCycle.Monthly)
            && p.Prices.Any(x => x.CurrencyCode == "USD" && x.Cycle == BillingCycle.Annual)
            && p.Prices.Any(x => x.CurrencyCode == "EUR" && x.Cycle == BillingCycle.Monthly)
            && p.Prices.Any(x => x.CurrencyCode == "EUR" && x.Cycle == BillingCycle.Annual));
        (await db.Products.CountAsync(p => p.Status == ProductStatus.Inactive)).Should().BeGreaterThan(0);
        (await db.TldConfigs.Select(t => t.SellCurrency).Distinct().ToListAsync()).Should().Contain(["USD", "EUR"]);
        (await db.TldConfigs.Where(t => t.IsEnabled).CountAsync()).Should().BeGreaterThanOrEqualTo(15);

        (await db.Clients.Select(c => c.Status).Distinct().ToListAsync())
            .Should().Contain([ClientStatus.Active, ClientStatus.Suspended, ClientStatus.Closed]);
        (await db.Invoices.Select(i => i.Status).Distinct().ToListAsync()).Should().Contain(
            [InvoiceStatus.Draft, InvoiceStatus.Unpaid, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Cancelled, InvoiceStatus.Refunded]);
        (await db.Invoices.Select(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month }).Distinct().CountAsync())
            .Should().BeGreaterThanOrEqualTo(12, "the revenue charts need a full year of history");
        (await db.ClientServices.Select(s => s.Status).Distinct().ToListAsync()).Should().Contain(
            [ServiceStatus.Pending, ServiceStatus.Active, ServiceStatus.Suspended, ServiceStatus.Terminated]);
        (await db.ClientServices.AnyAsync(s => s.ServerId != null)).Should().BeTrue();
        (await db.Orders.Select(o => o.Status).Distinct().ToListAsync()).Should().Contain(
            [OrderStatus.Pending, OrderStatus.Active, OrderStatus.Cancelled, OrderStatus.Fraud]);
        (await db.Quotes.Select(q => q.Stage).Distinct().ToListAsync()).Should().HaveCount(7);
        (await db.Quotes.AllAsync(q => q.Items.Count > 0)).Should().BeTrue();
        (await db.Tickets.Select(t => t.Status).Distinct().ToListAsync())
            .Should().Contain([TicketStatus.Open, TicketStatus.AwaitingReply, TicketStatus.Closed]);
        (await db.NetworkIssues.Select(n => n.Status).Distinct().ToListAsync()).Should().HaveCount(4);

        var thread = await db.Tickets.Include(t => t.Replies).OrderByDescending(t => t.Replies.Count).FirstAsync();
        thread.Replies.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    /// <summary>Domains cover every state, including one close to expiry and one waiting for a transfer, with their zones.</summary>
    [Fact]
    public async Task SeededDomains_CoverEveryStatusAndStayUnderReservedNamesAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Domains.Select(d => d.Status).Distinct().ToListAsync()).Should().Contain(
            [DomainStatus.Active, DomainStatus.PendingRegistration, DomainStatus.PendingTransfer, DomainStatus.Expired,
             DomainStatus.Redemption, DomainStatus.Transferred, DomainStatus.Cancelled]);
        var soon = DateTimeOffset.UtcNow.AddDays(30);
        (await db.Domains.AnyAsync(d => d.Status == DomainStatus.Active && d.ExpiresAt < soon)).Should().BeTrue("one is expiring soon");
        (await db.Domains.Include(d => d.DnsRecords).AnyAsync(d => d.DnsRecords.Count >= 4)).Should().BeTrue();
        (await db.Domains.AnyAsync(d => d.EmailForwarding)).Should().BeTrue();

        // Seeded hosts must not point at real sites: every name sits under the reserved .example suffix.
        (await db.Domains.Select(d => d.Name).ToListAsync()).Should().OnlyContain(n => n.EndsWith(".example", StringComparison.Ordinal));
        (await db.Servers.Select(s => s.Hostname).ToListAsync()).Should().OnlyContain(n => n.EndsWith(".example", StringComparison.Ordinal));
    }

    /// <summary>The demo client has a full profile, and the storefront settings carry the demo's identity.</summary>
    [Fact]
    public async Task DemoClientAndPortal_AreFullyPopulatedAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var demoClient = await db.Clients.Include(c => c.Contacts).Include(c => c.Users)
            .FirstAsync(c => c.CompanyName == "Brightleaf Studio LLC");
        demoClient.Contacts.Should().HaveCount(3);
        demoClient.Users.Should().HaveCount(2);
        demoClient.CardGatewayCustomerId.Should().NotBeNullOrEmpty();
        demoClient.CreditBalance.Should().BeGreaterThan(0);
        (await db.Invitations.CountAsync(i => i.ClientId == demoClient.Id && i.AcceptedAt == null)).Should().Be(2);
        (await db.ClientServices.CountAsync(s => s.ClientId == demoClient.Id)).Should().BeGreaterThan(0);
        (await db.Domains.CountAsync(d => d.ClientId == demoClient.Id)).Should().BeGreaterThan(0);

        // The client area lists mail by the account's sign-in address, so the demo client's history is not empty.
        (await db.EmailLogs.CountAsync(l => l.To == DemoTestHost.ClientEmail)).Should().BeGreaterThan(0);
        (await db.SubjectRoles.Select(r => r.Role).Distinct().ToListAsync()).Should().Contain([Role.Admin, Role.Reseller, Role.Client]);

        var settings = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        settings[PortalSettingKeys.SiteName].Should().Be("Hostpanel Demo");
        settings[PortalSettingKeys.Template].Should().Be(PortalSettingKeys.TemplateValue(PortalSettingKeys.DefaultTemplate), "the demo shows the default template");
        settings[PortalSettingKeys.ContactEmail].Should().Be(DemoPortalSettingsSeeder.DefaultContactEmail, "no contact email is configured");
        settings[PortalSettingKeys.ContactPhone].Should().Be(DemoPortalSettingsSeeder.DefaultContactPhone, "no contact phone is configured");
        settings[PortalSettingKeys.SiteTagline].Should().NotBeNullOrEmpty();
        settings[PortalSettingKeys.ContactMapUrl].Should().Be(DemoStorefrontSeeder.MapEmbedUrl, "the contact map must be a real embeddable page");
        settings[PortalSettingKeys.ContactAddress].Should().Be("1 Market Street, Fairhaven", "one seeder owns the contact address");
        settings[PortalSettingKeys.StatsClients].Should().Be("500+", "one seeder owns the hero figures");
        settings[PortalSettingKeys.ContactHours].Should().NotBeNullOrEmpty("the contact page shows its hours row");
        settings[PortalSettingKeys.StatsClients].Should().NotBeNullOrEmpty("the hero shows its figures");
        new[]
            {
                PortalSettingKeys.StatsResponseTime, PortalSettingKeys.StatsSitesHosted, PortalSettingKeys.StatsSupportResponse,
                PortalSettingKeys.InstallerAppCount, PortalSettingKeys.GuaranteeMoneyBackDays, PortalSettingKeys.SupportResponseChat,
                PortalSettingKeys.SupportResponseTicket, PortalSettingKeys.SupportResponseEmail, PortalSettingKeys.SupportResponsePhone,
                PortalSettingKeys.ChatProvider,
            }
            .Should().OnlyContain(key => !string.IsNullOrEmpty(settings[key]), "every hosting fact and support channel is filled");
        new[]
            {
                PortalSettingKeys.FeaturesSsl, PortalSettingKeys.FeaturesDdos, PortalSettingKeys.FeaturesBackups,
                PortalSettingKeys.FeaturesMalware, PortalSettingKeys.FeaturesFirewall, PortalSettingKeys.GuaranteeNoHiddenFees,
                PortalSettingKeys.MigrationFree,
            }
            .Should().OnlyContain(key => settings[key] == "true", "every security and guarantee card is on");
        settings.Where(s => s.Key.StartsWith("portal.sections.", StringComparison.Ordinal))
            .Should().HaveCount(20).And.OnlyContain(s => s.Value == "true", "every home-page section is on in the demo");
        new[] { PortalSettingKeys.AboutText, PortalSettingKeys.AboutTextRu, PortalSettingKeys.AboutTextHy }
            .Should().OnlyContain(k => settings[k].Length > 0, "the About page has a story in every language");
        settings[PortalSettingKeys.BrandPrimary].Should().BeEmpty("the demo shows the template's own palette");
        settings[PortalSettingKeys.BrandAccent].Should().BeEmpty("the demo shows the template's own palette");

        var slides = await db.Slides.Include(s => s.Translations).ToListAsync();
        slides.Should().HaveCountGreaterThanOrEqualTo(5);
        slides.Should().OnlyContain(s => s.Translations.Select(t => t.Locale).OrderBy(l => l).SequenceEqual(new[] { "en", "hy", "ru" }));
    }

    /// <summary>The published accounts are real: both sign in with the published passwords.</summary>
    [Fact]
    public async Task PublishedAccounts_SignInAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().SeedAsync(CancellationToken.None);
        }

        // One client per sign-in: the first sign-in sets the session cookie, and a second state-changing
        // request carrying that cookie without the CSRF header is refused by the local-session guard.
        var admin = await host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = DemoTestHost.AdminEmail, password = DemoTestHost.AdminPassword });
        var customer = await host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = DemoTestHost.ClientEmail, password = DemoTestHost.ClientPassword });

        admin.StatusCode.Should().Be(HttpStatusCode.OK);
        customer.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The storefront shows a full testimonial carousel: at least nine translated testimonials rated four or five,
    /// some with a bundled portrait and some without, so both the picture and the initials fallback appear.
    /// </summary>
    [Fact]
    public async Task Seed_FillsTheTestimonialCarouselAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testimonials = await db.Testimonials.Include(t => t.Translations).ToListAsync();
        testimonials.Should().HaveCountGreaterThanOrEqualTo(9);
        testimonials.Should().OnlyContain(t => t.IsActive && t.Rating >= 4 && t.Rating <= 5 && t.Translations.Count == 3);
        testimonials.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        testimonials.Should().Contain(t => t.AvatarUrl != null && t.AvatarUrl.StartsWith("/demo/avatars/"));
        testimonials.Should().Contain(t => t.AvatarUrl == null);
    }

    /// <summary>
    /// Every demo partner has its own bundled logo file named after the partner, so the partner strips show logos,
    /// and the renamed security partner carries its new name in all three languages.
    /// </summary>
    [Fact]
    public async Task Seed_GivesEveryPartnerItsOwnLogoAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var partners = await db.Partners.Include(p => p.Translations).ToListAsync();
        partners.Should().OnlyContain(p => p.LogoUrl != null && p.LogoUrl.StartsWith("/demo/partners/") && p.LogoUrl.EndsWith(".svg"));
        partners.Select(p => p.LogoUrl).Should().OnlyHaveUniqueItems();
        partners.SelectMany(p => p.Translations).Select(t => t.Name).Where(name => name.Contains("Vantor") || name.Contains("Вантор") || name.Contains("Վանտոր")).Should().HaveCount(3);
        partners.Should().Contain(p => p.LogoUrl == "/demo/partners/vantor-security.svg");
    }

    /// <summary>Every demo project has its own bundled preview picture named after its slug.</summary>
    [Fact]
    public async Task Seed_GivesEveryProjectItsOwnPreviewImageAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var projects = await db.Projects.ToListAsync();
        projects.Should().NotBeEmpty();
        projects.Should().OnlyContain(p => p.ImageUrls.Length == 1);
        projects.Select(p => p.ImageUrls[0]).Should().BeEquivalentTo(projects.Select(p => $"/demo/projects/{p.Slug}.svg"));
        projects.Select(p => p.ImageUrls[0]).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Every demo slide carries its own bundled cover picture, so the slider never falls back to a blank cover.</summary>
    /// <returns>A task that completes when the check has run.</returns>
    [Fact]
    public async Task Seed_GivesEverySlideItsOwnCoverImageAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slides = await db.Slides.ToListAsync();
        slides.Should().NotBeEmpty();
        slides.Should().OnlyContain(s => s.ImageUrl.StartsWith("/demo/slides/", StringComparison.Ordinal) && s.ImageUrl.EndsWith(".svg", StringComparison.Ordinal));
        slides.Select(s => s.ImageUrl).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The content-only entry the development seeder uses adds nothing to tables that already hold rows and never
    /// touches the settings, so a developer's local edits survive a restart.
    /// </summary>
    [Fact]
    public async Task SeedContent_OnPopulatedTables_ChangesNothingAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await ResetAsync(host);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var badge = await db.Settings.SingleAsync(s => s.Key == PortalSettingKeys.HeroBadge);
        badge.UpdateValue("Edited locally");
        await db.SaveChangesAsync();
        var before = (await db.Testimonials.CountAsync(), await db.Partners.CountAsync(), await db.ServerLocations.CountAsync(), await db.Projects.CountAsync(), await db.Faqs.CountAsync());

        await new DemoStorefrontSeeder(db, DemoAnchor.Today(DateTimeOffset.UtcNow)).SeedContentAsync(CancellationToken.None);

        (await db.Testimonials.CountAsync(), await db.Partners.CountAsync(), await db.ServerLocations.CountAsync(), await db.Projects.CountAsync(), await db.Faqs.CountAsync())
            .Should().Be(before);
        (await db.Settings.SingleAsync(s => s.Key == PortalSettingKeys.HeroBadge)).Value.Should().Be("Edited locally");
    }

    /// <summary>A deployment that is not a demo is never seeded, even if the manager is asked.</summary>
    [Fact]
    public async Task WhenDisabled_SeedsNothingAsync()
    {
        var host = DemoTestHost.EnabledThenSwitchedOff(factory);
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IDemoDataManager>();
        manager.Should().BeOfType<DemoDataManager>("the Enabled guard under test lives in the real manager");
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var currenciesBefore = await db.Currencies.CountAsync();
        var domainsBefore = await db.Domains.CountAsync();

        await manager.SeedAsync(CancellationToken.None);

        (await db.Currencies.CountAsync()).Should().Be(currenciesBefore);
        (await db.Domains.CountAsync()).Should().Be(domainsBefore);
    }

    /// <summary>A configured contact pair replaces the fictional one on the storefront, and a later seed keeps it.</summary>
    [Fact]
    public async Task ConfiguredContact_IsWhatThePortalShowsAsync()
    {
        var host = DemoTestHost.Enabled(factory).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Demo:ContactEmail", "contact@demo.example");
            builder.UseSetting("Demo:ContactPhone", "+1 555 0199");
        });
        await ResetAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            // A plain seed over existing rows (what a restart runs) must still write the configured values.
            await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().SeedAsync(CancellationToken.None);
        }

        using var readScope = host.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        settings[PortalSettingKeys.ContactEmail].Should().Be("contact@demo.example");
        settings[PortalSettingKeys.ContactPhone].Should().Be("+1 555 0199");
    }
}
