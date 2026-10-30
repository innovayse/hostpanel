using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CurrencyEntity = Innovayse.Hostpanel.Domain.Billing.Currency;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds what a storefront sells and prices it in: currencies with their exchange rates, product
/// groups and products priced in USD and EUR for both billing cycles the domain knows (monthly and
/// annual), one feature list per product whose labels are identical across the plans of a group so a comparison table is never sparse, and TLD pricing. Every row goes through the entity's
/// <c>Create</c> and behaviour methods, never a setter, and each dataset is added only when its
/// table is empty so a second run changes nothing.
/// </summary>
internal sealed class DemoCatalogueSeeder
{
    /// <summary>The code of the base currency every rate is quoted against.</summary>
    internal const string BaseCurrency = "USD";

    /// <summary>The code of the second currency the catalogue is also priced in.</summary>
    internal const string LocalCurrency = "EUR";

    /// <summary>How many euros one dollar buys; the fixed demo exchange rate.</summary>
    internal const decimal EurosPerDollar = 0.92m;

    /// <summary>The amount, in euros, subtracted from a rounded converted price so it ends in .99 like a list price.</summary>
    private const decimal PsychologicalStep = 0.01m;

    /// <summary>Separates the features of one product in the catalogue table.</summary>
    private const char FeatureSeparator = ';';

    /// <summary>Separates a feature's label from its value in the catalogue table.</summary>
    private const char FeatureValueSeparator = '=';

    /// <summary>
    /// Slugs of the plans marked as their group's recommended plan: the middle tier of the hosting
    /// plans and of the VPS plans. Exactly one per group, as the domain expects.
    /// </summary>
    internal static readonly IReadOnlySet<string> FeaturedSlugs = new HashSet<string>
    {
        "managed-site-hosting",
        "vps-pro",
    };

    /// <summary>The SKU of the demo digital product, so the product form's SKU field has a value to show.</summary>
    internal const string DigitalSku = "CREDITS-100";

    /// <summary>The metadata (a JSON object) of the demo digital product.</summary>
    internal const string DigitalMetadata = "{\"credits\": 100, \"delivery\": \"instant\"}";

    /// <summary>Days the demo digital product stays usable after its due date before it is suspended.</summary>
    internal const int DigitalGracePeriodDays = 14;

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor the registrar price sync is stamped with.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor the registrar price sync is stamped with.</param>
    public DemoCatalogueSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Whether a product runs on a hosting account, so it names the panel package its accounts are created with.</summary>
    /// <param name="type">The product type.</param>
    /// <returns><see langword="true"/> for shared hosting, VPS and dedicated products.</returns>
    internal static bool IsHosting(ProductType type) => type is ProductType.SharedHosting or ProductType.Vps or ProductType.Dedicated;

    /// <summary>Converts a dollar price to the euro list price, rounded to a whole euro less one cent (x.99).</summary>
    /// <param name="usd">The price in dollars.</param>
    /// <returns>The price in euros, ending in .99, or zero when the dollar price is zero.</returns>
    internal static decimal ToEuros(decimal usd) =>
        usd <= 0m
            ? 0m
            : Math.Max(1m, Math.Round(usd * EurosPerDollar, MidpointRounding.AwayFromZero)) - PsychologicalStep;

    /// <summary>Seeds currencies, products and TLD pricing that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await _db.Currencies.AnyAsync(ct))
        {
            _db.Currencies.Add(CurrencyEntity.Create(BaseCurrency, "840", "$", string.Empty, 2, 1m, true));

            // One euro is worth 1/0.92 of a dollar; the rate column keeps eight decimals.
            _db.Currencies.Add(CurrencyEntity.Create(
                LocalCurrency, "978", "€", string.Empty, 2, Math.Round(1m / EurosPerDollar, 8), false));
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.ProductGroups.AnyAsync(ct))
        {
            await SeedProductsAsync(ct);
        }

        if (!await _db.TldConfigs.AnyAsync(ct))
        {
            await SeedTldsAsync(ct);
        }
    }

    /// <summary>Seeds product groups and their products, each priced monthly and annually in both currencies.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedProductsAsync(CancellationToken ct)
    {
        (string Group, string Description, ProductType Type, (string Name, string Slug, string Blurb, decimal Monthly, decimal Annual, string Features)[] Products)[] catalogue =
        [
            ("Web Hosting", "Fast shared hosting for sites and small shops.", ProductType.SharedHosting,
            [
                ("Starter Hosting", "starter-hosting", "One site with 10 GB SSD storage and free SSL.", 4.99m, 49.99m,
                    "Websites=1;Storage=10 GB SSD;Bandwidth=100 GB;Email accounts=5;Backups=Weekly;SSL=Free SSL;Support=Standard;Client accounts=1;White label=No"),
                ("Business Hosting", "business-hosting", "Ten sites with 50 GB SSD storage and daily backups.", 9.99m, 99.99m,
                    "Websites=10;Storage=50 GB SSD;Bandwidth=500 GB;Email accounts=25;Backups=Daily;SSL=Free SSL;Support=Standard;Client accounts=1;White label=No"),
                ("Pro Hosting", "pro-hosting", "Unlimited sites with 150 GB SSD and priority support.", 19.99m, 199.99m,
                    "Websites=Unlimited;Storage=150 GB SSD;Bandwidth=Unmetered;Email accounts=100;Backups=Daily;SSL=Free SSL;Support=Priority;Client accounts=1;White label=No"),
                ("Reseller Hosting", "reseller-hosting", "Fifty client accounts with 300 GB SSD and white label.", 34.99m, 349.99m,
                    "Websites=Unlimited;Storage=300 GB SSD;Bandwidth=Unmetered;Email accounts=Unlimited;Backups=Daily;SSL=Free SSL;Support=Priority;Client accounts=50;White label=Yes"),
                ("Managed Site Hosting", "managed-site-hosting", "Twenty-five managed sites with automatic updates.", 14.99m, 149.99m,
                    "Websites=25;Storage=75 GB SSD;Bandwidth=1 TB;Email accounts=50;Backups=Daily;SSL=Free SSL;Support=Priority;Client accounts=1;White label=No"),
            ]),
            ("Cloud VPS", "Isolated virtual servers with full root access.", ProductType.Vps,
            [
                ("VPS Basic", "vps-basic", "2 vCPU, 4 GB RAM, 80 GB NVMe.", 29.99m, 299.99m,
                    "CPU=2 vCPU;Memory=4 GB RAM;Storage=80 GB NVMe;Bandwidth=4 TB;Backups=Weekly;Support=Standard"),
                ("VPS Pro", "vps-pro", "4 vCPU, 8 GB RAM, 160 GB NVMe.", 59.99m, 599.99m,
                    "CPU=4 vCPU;Memory=8 GB RAM;Storage=160 GB NVMe;Bandwidth=6 TB;Backups=Daily;Support=Standard"),
                ("VPS Business", "vps-business", "6 vCPU, 16 GB RAM, 320 GB NVMe.", 99.99m, 999.99m,
                    "CPU=6 vCPU;Memory=16 GB RAM;Storage=320 GB NVMe;Bandwidth=8 TB;Backups=Daily;Support=Priority"),
                ("VPS Enterprise", "vps-enterprise", "8 vCPU, 32 GB RAM, 640 GB NVMe.", 179.99m, 1799.99m,
                    "CPU=8 vCPU;Memory=32 GB RAM;Storage=640 GB NVMe;Bandwidth=10 TB;Backups=Daily;Support=Priority"),
                ("VPS Mini", "vps-mini", "1 vCPU, 2 GB RAM, 40 GB NVMe.", 14.99m, 149.99m,
                    "CPU=1 vCPU;Memory=2 GB RAM;Storage=40 GB NVMe;Bandwidth=2 TB;Backups=Weekly;Support=Standard"),
            ]),
            ("Dedicated Servers", "Single-tenant hardware for demanding workloads.", ProductType.Dedicated,
            [
                ("Dedicated Starter", "dedicated-starter", "8 cores, 32 GB RAM, 2 x 480 GB SSD.", 89.99m, 899.99m,
                    "CPU=8 cores;Memory=32 GB RAM;Storage=2 x 480 GB SSD;Bandwidth=20 TB;Graphics=None;Support=Standard"),
                ("Dedicated Plus", "dedicated-plus", "12 cores, 64 GB RAM, 2 x 960 GB SSD.", 149.99m, 1499.99m,
                    "CPU=12 cores;Memory=64 GB RAM;Storage=2 x 960 GB SSD;Bandwidth=30 TB;Graphics=None;Support=Priority"),
                ("Dedicated Max", "dedicated-max", "24 cores, 128 GB RAM, 4 x 960 GB NVMe.", 259.99m, 2599.99m,
                    "CPU=24 cores;Memory=128 GB RAM;Storage=4 x 960 GB NVMe;Bandwidth=50 TB;Graphics=None;Support=24/7"),
                ("Dedicated GPU", "dedicated-gpu", "16 cores, 128 GB RAM and one data-centre GPU.", 449.99m, 4499.99m,
                    "CPU=16 cores;Memory=128 GB RAM;Storage=2 x 960 GB NVMe;Bandwidth=50 TB;Graphics=1 data-centre GPU;Support=24/7"),
                ("Dedicated Legacy", "dedicated-legacy", "Previous generation hardware, no longer sold.", 69.99m, 699.99m,
                    "CPU=4 cores;Memory=16 GB RAM;Storage=2 x 240 GB SSD;Bandwidth=10 TB;Graphics=None;Support=Standard"),
            ]),
            ("SSL Certificates", "Certificates that put the padlock on your site.", ProductType.Ssl,
            [
                ("SSL Basic", "ssl-basic", "Domain-validated certificate for one name.", 9.99m, 89.99m,
                    "Validation=Domain;Names=1;Warranty=$10,000"),
                ("SSL Wildcard", "ssl-wildcard", "Domain-validated certificate for all subdomains.", 29.99m, 279.99m,
                    "Validation=Domain;Names=Unlimited subdomains;Warranty=$50,000"),
                ("SSL Extended", "ssl-extended", "Organisation-validated certificate with a verified identity.", 79.99m, 749.99m,
                    "Validation=Organisation;Names=1;Warranty=$250,000"),
                ("SSL Multi-Domain", "ssl-multi-domain", "One certificate for up to five different names.", 39.99m, 379.99m,
                    "Validation=Domain;Names=Up to 5;Warranty=$50,000"),
            ]),
            ("Business Email", "Professional mailboxes on your own domain.", ProductType.Email,
            [
                ("Mail Starter", "mail-starter", "5 mailboxes with 10 GB each.", 2.99m, 29.99m,
                    "Mailboxes=5;Storage=10 GB each;Spam filter=Included;Calendars=Personal;Archiving=No"),
                ("Mail Business", "mail-business", "25 mailboxes with 30 GB each and shared calendars.", 5.99m, 59.99m,
                    "Mailboxes=25;Storage=30 GB each;Spam filter=Included;Calendars=Shared;Archiving=No"),
                ("Mail Enterprise", "mail-enterprise", "Unlimited mailboxes with archiving and audit logs.", 11.99m, 119.99m,
                    "Mailboxes=Unlimited;Storage=100 GB each;Spam filter=Included;Calendars=Shared;Archiving=Included"),
                ("Mail Archive", "mail-archive", "Ten-year compliant archive for every mailbox on a domain.", 4.99m, 49.99m,
                    "Mailboxes=Every mailbox;Storage=Unlimited;Spam filter=No;Calendars=No;Archiving=10 years"),
            ]),
            ("Domains", "Register, transfer and manage your domain names.", ProductType.Domain,
            [
                ("Domain Registration", "domain-registration", "Register, renew and transfer domains.", 0m, 0m,
                    "DNS=Free management;Security=Registrar lock;Renewal=Automatic"),
                ("Domain Transfer", "domain-transfer", "Move a domain to us and extend it by a year.", 0m, 0m,
                    "DNS=Free management;Security=Registrar lock;Renewal=Extended one year"),
                ("WHOIS Privacy", "whois-privacy", "Keep your contact details out of the public record.", 0.99m, 9.99m,
                    "DNS=Not included;Security=Contact details hidden;Renewal=With the domain"),
                ("Premium DNS", "premium-dns", "Anycast name servers with DNSSEC and a 100% uptime target.", 1.99m, 19.99m,
                    "DNS=Anycast name servers;Security=DNSSEC;Renewal=Annual"),
            ]),
            ("Add-ons", "Extras that can be attached to a service.", ProductType.Addon,
            [
                ("Dedicated IP", "dedicated-ip", "A private IP address for one service.", 2.99m, 29.99m,
                    "Billing=Per service;Setup=Instant;Availability=IPv4 address"),
                ("Daily Backups", "daily-backups", "Offsite backups every night, kept for 30 days.", 3.99m, 39.99m,
                    "Billing=Per service;Setup=Instant;Availability=30 days retention"),
                ("Priority Support", "priority-support", "Skip the queue with a one-hour response target.", 14.99m, 149.99m,
                    "Billing=Per account;Setup=Instant;Availability=24/7"),
                ("Site Monitoring", "site-monitoring", "Uptime checks every minute with alerts by mail and text.", 4.99m, 49.99m,
                    "Billing=Per site;Setup=Instant;Availability=Every minute"),
            ]),
            ("Digital Goods", "Licences and credit packs delivered on payment, nothing to provision.", ProductType.Digital,
            [
                ("Credit Pack", "credit-pack", "One hundred credits delivered instantly after payment.", 9.99m, 99.99m,
                    "Delivery=Instant;Licence=Single user;Support=Standard"),
            ]),
        ];

        foreach (var (groupName, groupDescription, type, products) in catalogue)
        {
            var group = ProductGroup.Create(groupName, groupDescription);
            _db.ProductGroups.Add(group);
            await _db.SaveChangesAsync(ct);

            // Plans read cheapest first inside their group; equal prices keep the table order.
            var positions = products
                .Select((p, index) => (p.Slug, p.Monthly, Index: index))
                .OrderBy(x => x.Monthly)
                .ThenBy(x => x.Index)
                .Select((x, position) => (x.Slug, Position: position + 1))
                .ToDictionary(x => x.Slug, x => x.Position);

            var created = new List<(Product Product, string Features)>();
            foreach (var (name, slug, blurb, monthly, annual, features) in products)
            {
                // The legacy columns carry the dollar figures for the rollback release; the
                // storefront reads the per-currency rows set below.
                // A hosting plan names the panel package its accounts are created with; provisioning refuses one without it.
                var product = Product.Create(
                    group.Id, name, blurb, null, slug, IsHosting(type) ? slug : null, type, monthly, annual, _today,
                    isFeatured: FeaturedSlugs.Contains(slug), sortOrder: positions[slug]);
                product.SetPrice(BaseCurrency, BillingCycle.Monthly, monthly);
                product.SetPrice(BaseCurrency, BillingCycle.Annual, annual);
                product.SetPrice(LocalCurrency, BillingCycle.Monthly, ToEuros(monthly));
                product.SetPrice(LocalCurrency, BillingCycle.Annual, ToEuros(annual));

                // The digital product shows the three fields only it uses: a SKU, JSON metadata and a grace period.
                if (type == ProductType.Digital)
                {
                    product.SetSku(DigitalSku);
                    product.SetMetadata(DigitalMetadata);
                    product.SetGracePeriodDays(DigitalGracePeriodDays);
                }

                // The one product that is no longer sold stays listed so the admin sees an inactive row.
                if (slug == "dedicated-legacy")
                {
                    product.Deactivate();
                }

                _db.Products.Add(product);
                created.Add((product, features));
            }

            // Saved first so each product has the id its feature rows point at.
            await _db.SaveChangesAsync(ct);

            foreach (var (product, features) in created)
            {
                var sortOrder = 0;
                foreach (var feature in features.Split(FeatureSeparator))
                {
                    var parts = feature.Split(FeatureValueSeparator);
                    _db.ProductFeatures.Add(ProductFeature.Create(product.Id, parts[0], parts[1], sortOrder));
                    sortOrder++;
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Seeds TLD pricing: dollar-priced global extensions across several categories and a euro-priced regional one.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedTldsAsync(CancellationToken ct)
    {
        (string Tld, string Currency, decimal Register, decimal Renew, decimal Transfer, string[] Categories, bool Enabled)[] tlds =
        [
            ("com", BaseCurrency, 11.99m, 14.99m, 11.99m, ["popular", "business"], true),
            ("net", BaseCurrency, 12.99m, 15.99m, 12.99m, ["popular", "business"], true),
            ("org", BaseCurrency, 11.49m, 14.49m, 11.49m, ["popular", "community"], true),
            ("info", BaseCurrency, 9.99m, 17.99m, 9.99m, ["budget"], true),
            ("biz", BaseCurrency, 10.99m, 16.99m, 10.99m, ["business"], true),
            ("io", BaseCurrency, 39.99m, 44.99m, 39.99m, ["tech", "popular"], true),
            ("co", BaseCurrency, 24.99m, 29.99m, 24.99m, ["business"], true),
            ("dev", BaseCurrency, 14.99m, 16.99m, 14.99m, ["tech"], true),
            ("app", BaseCurrency, 16.99m, 18.99m, 16.99m, ["tech"], true),
            ("xyz", BaseCurrency, 2.99m, 13.99m, 2.99m, ["budget", "creative"], true),
            ("online", BaseCurrency, 4.99m, 34.99m, 4.99m, ["budget", "creative"], true),
            ("site", BaseCurrency, 3.99m, 29.99m, 3.99m, ["budget"], true),
            ("store", BaseCurrency, 5.99m, 49.99m, 5.99m, ["shopping"], true),
            ("shop", BaseCurrency, 7.99m, 32.99m, 7.99m, ["shopping"], true),
            ("tech", BaseCurrency, 8.99m, 44.99m, 8.99m, ["tech"], true),
            ("cloud", BaseCurrency, 12.99m, 21.99m, 12.99m, ["tech"], true),
            ("eu", BaseCurrency, 7.99m, 9.99m, 7.99m, ["regional"], true),
            ("de", LocalCurrency, 8.99m, 10.99m, 8.99m, ["regional"], true),
            ("xxx", BaseCurrency, 89.99m, 94.99m, 89.99m, ["adult"], false),
        ];

        var sortOrder = 0;
        foreach (var (tld, currency, register, renew, transfer, categories, enabled) in tlds)
        {
            var costFactor = 0.8m;
            var config = TldConfig.Create(tld, RegistrarModule.NameAm, currency, currency, enabled, sortOrder, [.. categories]);
            config.UpdateCostPrices(
                new Dictionary<int, decimal> { [1] = Math.Round(register * costFactor, 2), [2] = Math.Round(register * costFactor * 2, 2) },
                new Dictionary<int, decimal> { [1] = Math.Round(transfer * costFactor, 2) },
                new Dictionary<int, decimal> { [1] = Math.Round(renew * costFactor, 2), [2] = Math.Round(renew * costFactor * 2, 2) });
            config.UpdateSellPrices(
                new Dictionary<int, decimal> { [1] = register, [2] = register * 2 },
                new Dictionary<int, decimal> { [1] = transfer },
                new Dictionary<int, decimal> { [1] = renew, [2] = renew * 2 });
            config.MarkSynced(_today);
            _db.TldConfigs.Add(config);
            sortOrder++;
        }

        await _db.SaveChangesAsync(ct);
    }
}
