using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Domain.Ssl;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Persistence.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// Seeds realistic test data for development. Idempotent: skips if clients already exist.
/// </summary>
public sealed class DevDataSeeder
{
    /// <summary>The database the development data is written to.</summary>
    private readonly AppDbContext _db;

    /// <summary>Creates the development sign-in accounts.</summary>
    private readonly UserManager<AppUser> _userManager;

    /// <summary>Discards the domain events seed saves raise, so seeding triggers no notifications.</summary>
    private readonly IDomainEventGate _eventGate;

    /// <summary>The sign-in passwords of the accounts this class creates.</summary>
    private readonly DevDataSeederOptions _passwords;

    /// <summary>Records which parts were seeded or skipped.</summary>
    private readonly ILogger<DevDataSeeder> _logger;

    /// <summary>The moment this seeding run counts every date from, read once from the clock.</summary>
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="DevDataSeeder"/> class.</summary>
    /// <param name="db">The database the development data is written to.</param>
    /// <param name="userManager">Creates the development sign-in accounts.</param>
    /// <param name="eventGate">Discards the domain events seed saves raise, so seeding triggers no notifications.</param>
    /// <param name="options">The sign-in passwords of the accounts this class creates, from configuration.</param>
    /// <param name="logger">Records which parts were seeded or skipped.</param>
    /// <param name="clock">The source of the moment the seeded dates count from.</param>
    public DevDataSeeder(AppDbContext db, UserManager<AppUser> userManager, IDomainEventGate eventGate, IOptions<DevDataSeederOptions> options, ILogger<DevDataSeeder> logger, IClock clock)
    {
        _db = db;
        _userManager = userManager;
        _eventGate = eventGate;
        _passwords = options.Value;
        _logger = logger;
        _now = clock.UtcNow;
    }

    /// <summary>Fixed-seed generator so every dev database gets the same pseudo-random figures.</summary>
    private static readonly Random Rng = new(42);

    /// <summary>
    /// Currency every seeded invoice bills in and every seeded product is priced in. Seeded
    /// clients carry no currency of their own and the prices above are dollar figures, so the
    /// seed names the currency those figures are in rather than resolving one per client.
    /// </summary>
    private const string SeedInvoiceCurrency = "USD";

    /// <summary>Builds a date in the middle of a past month, relative to now.</summary>
    /// <param name="now">The moment the months are counted back from.</param>
    /// <param name="months">How many whole months back from the current month.</param>
    /// <param name="day">Day of that month, 1-based.</param>
    /// <returns>Midnight UTC on the requested day.</returns>
    private static DateTimeOffset MonthsAgo(DateTimeOffset now, int months, int day = 15) =>
        new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
            .AddMonths(-months)
            .AddDays(day - 1);

    /// <summary>Seeds test clients, invoices, transactions, quotes and billable items.</summary>
    /// <param name="ct">Cancels the request.</param>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Seeded rows are fixtures, not business events: no welcome mails, and the seed runs before
        // the message bus has started.
        using var suppression = _eventGate.Suppress();

        // The storefront content (testimonials, partners, projects, FAQs) is the same fictional set the demo
        // ships, filled into whichever of those tables are still empty; one source, so the two never drift.
        await new DemoStorefrontSeeder(_db, DemoAnchor.Today(_now)).SeedContentAsync(ct);

        // ── Admin user ───────────────────────────────────────────────────────
        const string adminEmail = "admin@example.test";
        if (await _userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
            };
            var adminResult = await _userManager.CreateAsync(adminUser, _passwords.AdminPassword);
            if (adminResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

        var productNames = await _db.Products.Select(p => p.Name).ToListAsync(ct);
        var hasProductInvoices = productNames.Count > 0 && await _db.InvoiceItems
            .AnyAsync(ii => productNames.Contains(ii.Description), ct);

        if (await _db.Clients.AnyAsync(ct) && await _db.Invoices.AnyAsync(ct) && await _db.Quotes.AnyAsync(ct)
            && await _db.ProductGroups.AnyAsync(ct) && await _db.Orders.AnyAsync(ct) && hasProductInvoices)
        {
            if (!await _db.Slides.AnyAsync(ct))
            {
                await SeedSlidesAsync(_db, _now, _logger, ct);
            }

            // Keyed on tickets rather than departments: DefaultDepartmentsSeeder fills the
            // departments table in every environment before this runs, so a departments check
            // would always be false and the KB and tickets would never be seeded.
            if (!await _db.Tickets.AnyAsync(ct))
            {
                await SeedSupportAsync(_db, _now, ct, _logger);
            }

            if (!await _db.Domains.AnyAsync(ct))
            {
                await SeedDomainsAsync(_db, _now, ct, _logger);
            }

            if (!await _db.ServerGroups.AnyAsync(ct))
            {
                await SeedServersAsync(_db, _now, ct, _logger);
            }

            if (!await _db.Settings.AnyAsync(ct))
            {
                await SeedSettingsAsync(_db, ct, _logger);
            }

            if (!await _db.Announcements.AnyAsync(ct))
            {
                await SeedAnnouncementsAsync(_db, ct, _logger);
            }

            if (!await _db.Products.AnyAsync(p => p.Type == ProductType.Domain, ct))
            {
                var domainGroup = await _db.ProductGroups.FirstOrDefaultAsync(g => g.Name == "Domains", ct);
                if (domainGroup is null)
                {
                    domainGroup = ProductGroup.Create("Domains", null);
                    _db.ProductGroups.Add(domainGroup);
                    await _db.SaveChangesAsync(ct);
                }

                var domainProduct = Product.Create(domainGroup.Id, "Domain Registration", null, null, null, null, ProductType.Domain, 0m, 0m, _now);
                domainProduct.SetPrice(SeedInvoiceCurrency, BillingCycle.Monthly, 0m);
                domainProduct.SetPrice(SeedInvoiceCurrency, BillingCycle.Annual, 0m);
                _db.Products.Add(domainProduct);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Seeded Domain Registration product");
            }

            _logger.LogInformation("Dev seed skipped — data already exists");
            return;
        }

        _logger.LogInformation("Seeding dev test data…");

        // ── Clients ──────────────────────────────────────────────────────────
        var clientDefs = new List<(string Email, string First, string Last, string? Company, string? Phone, string? Street, string? City, string? State, string? PostCode, string? Country, ClientStatus Status)>
        {
            ("owner@example.test",     "Owner",     "One",     "Owner Enterprises",    "+1-555-0101", "123 Main St",        "New York",      "NY",  "10001",    "US", ClientStatus.Active),
            ("owner2@example.test",    "Owner",     "Two",     "Owner & Co",           "+1-555-0102", "456 Oak Ave",        "San Francisco", "CA",  "94102",    "US", ClientStatus.Active),
            ("owner3@example.test",    "Owner",     "Three",   "DigiTech Solutions",   "+1-555-0141", "1 Harbour St",  "Fairhaven",       null,  "0001",     "AM", ClientStatus.Active),
            ("customer@example.test",  "Customer",  "One",     null,                   "+1-555-0142", "2 Harbour St",  "Fairhaven",        null,  "28013",    "ES", ClientStatus.Active),
            ("customer2@example.test", "Customer",  "Two",     "Customer Web Solutions", "+1-555-0143", "3 Harbour St", "Fairhaven",     null,  "SW1A 2AA", "GB", ClientStatus.Active),
            ("customer3@example.test", "Customer",  "Three",   null,                   "+1-555-0106", "789 Pine Ln",        "Chicago",       "IL",  "60601",    "US", ClientStatus.Suspended),
            ("customer4@example.test", "Customer",  "Four",    "Customer Hosting",     "+1-555-0107", "321 Elm St",         "Austin",        "TX",  "73301",    "US", ClientStatus.Active),
            ("customer5@example.test", "Customer",  "Five",    "Customer Digital",     "+1-555-0144", "4 Harbour St",  "Fairhaven",         null,  "06000",    "KR", ClientStatus.Active),
            ("developer@example.test", "Developer", "One",     null,                   "+1-555-0145", "5 Harbour St", "Fairhaven",     null,  "75001",    "FR", ClientStatus.Inactive),
            ("developer2@example.test","Developer", "Two",     "Dev Media",            "+1-555-0146", "6 Harbour St",   "Fairhaven",        "NSW", "2000",     "AU", ClientStatus.Active),
            ("manager@example.test",   "Manager",   "One",     null,                   null,          null,                 null,            null,  null,       null, ClientStatus.Active),
            ("manager2@example.test",  "Manager",   "Two",     "Manager Corp",         "+1-555-0112", "555 Broadway",       "New York",      "NY",  "10012",    "US", ClientStatus.Active),
            ("partner@example.test",   "Partner",   "One",     null,                   "+1-555-0147", "7 Harbour St", "Fairhaven",   null,  "10117",    "DE", ClientStatus.Active),
            ("reseller@example.test",  "Reseller",  "One",     "Reseller Cloud Hosting", "+1-555-0114", "900 Cloud Dr",   "Seattle",       "WA",  "98101",    "US", ClientStatus.Closed),
            ("reseller2@example.test", "Reseller",  "Two",     null,                   "+1-555-0115", "77 Sunset Blvd",     "Los Angeles",   "CA",  "90028",    "US", ClientStatus.Active),
            ("customer6@example.test", "Customer",  "Six",     "Six Tech Ltd",         "+1-555-0148", "8 Harbour St",     "Fairhaven",       "ON",  "M5J 2J3",  "CA", ClientStatus.Active),
            ("customer7@example.test", "Customer",  "Seven",   null,                   "+1-555-0149", "9 Harbour St", "Fairhaven", null, "01310-100", "BR", ClientStatus.Active),
            ("developer3@example.test","Developer", "Three",   "Dev Studio AM",        "+1-555-0150", "10 Harbour St", "Fairhaven",       null,  "0019",     "AM", ClientStatus.Active),
            ("partner2@example.test",  "Partner",   "Two",     "Partner Solutions",    "+1-555-0151", "11 Harbour St", "Fairhaven",      null,  "00000",    "AE", ClientStatus.Active),
            ("reseller3@example.test", "Reseller",  "Three",   "Reseller Networks",    "+1-555-0152", "12 Harbour St",  "Fairhaven",     null,  "048616",   "SG", ClientStatus.Suspended),
        };

        if (!await _db.Clients.AnyAsync(ct))
        {
            var clientIdx = 0;
            foreach (var (email, first, last, company, phone, street, city, state, postCode, country, status) in clientDefs)
            {
                // An account that already exists (an earlier run stopped half way, or a person signed up
                // with this address) keeps its password; the client record is created for it all the same.
                var user = await _userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    user = new AppUser { UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
                    var result = await _userManager.CreateAsync(user, _passwords.ClientPassword);
                    if (!result.Succeeded)
                    {
                        continue;
                    }
                }

                if (!await _userManager.IsInRoleAsync(user, Roles.Client))
                {
                    await _userManager.AddToRoleAsync(user, Roles.Client);
                }

                var client = Client.Create(user.Id, first, last, email, _now, company);
                if (phone is not null)
                {
                    client.Update(first, last, company, phone);
                }

                if (street is not null)
                {
                    client.UpdateAddress(street, null, city, state, postCode, country);
                }

                if (status == ClientStatus.Suspended)
                {
                    client.Suspend(_now);
                }

                _db.Clients.Add(client);

                var signupMonthsBack = 5 - (clientIdx * 6 / clientDefs.Count);
                client.Backdate(MonthsAgo(_now, signupMonthsBack, Rng.Next(1, 28)));
                clientIdx++;

                await _db.SaveChangesAsync(ct);

                if (company is not null)
                {
                    client.AddContact(new ContactDetails(
                        first, last, company, email, phone, ContactType.Billing,
                        new PostalAddress(null, null, null, null, null, null),
                        new ContactNotifications(true, true, true, true, true, true)));
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        var clients = await _db.Clients.ToListAsync(ct);

        // ── Invoices spread across 6 months ─────────────────────────────────
        if (!await _db.Quotes.AnyAsync(ct))
        {
            var invoices = new List<Invoice>();

            string[] products = ["Shared Hosting Basic", "Shared Hosting Pro", "VPS Server", "Dedicated Server", "SSL Certificate", "Domain Registration", "Email Hosting", "CDN Service", "Backup Service", "Firewall Service"];
            decimal[] prices = [9.99m, 29.99m, 49.99m, 99.99m, 14.99m, 12.99m, 19.99m, 24.99m, 7.99m, 39.99m];

            foreach (var client in clients)
            {
                for (int monthsBack = 5; monthsBack >= 0; monthsBack--)
                {
                    var invoiceDate = MonthsAgo(_now, monthsBack, Rng.Next(1, 20));
                    var dueDate = invoiceDate.AddDays(30);
                    var productIndex = Rng.Next(products.Length);

                    var invoice = Invoice.Create(client.Id, dueDate, SeedInvoiceCurrency, invoiceDate);
                    invoice.AddItem(products[productIndex], prices[productIndex], 1);

                    if (Rng.Next(3) == 0)
                    {
                        var idx2 = Rng.Next(products.Length);
                        invoice.AddItem(products[idx2], prices[idx2], 1);
                    }

                    if (monthsBack >= 4)
                    {
                        invoice.RecordPaidHistory("txn_" + Rng.Next(100000, 999999), _now);
                    }
                    else if (monthsBack == 3)
                    {
                        if (Rng.Next(2) == 0)
                        {
                            invoice.RecordPaidHistory("txn_" + Rng.Next(100000, 999999), _now);
                        }
                        else
                        {
                            invoice.MarkOverdue(_now);
                        }
                    }
                    else if (monthsBack == 2)
                    {
                        if (Rng.Next(3) == 0)
                        {
                            invoice.MarkOverdue(_now);
                        }
                    }

                    _db.Invoices.Add(invoice);
                    invoices.Add(invoice);
                    invoice.Backdate(invoiceDate);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} invoices across 6 months", invoices.Count);
        }

        // ── Invoice Transactions ──────────────────────────────────────────────
        if (!await _db.InvoiceTransactions.AnyAsync(ct))
        {
            var paidInvoices = await _db.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid)
                .ToListAsync(ct);

            foreach (var inv in paidInvoices)
            {
                if (inv.Total <= 0)
                {
                    continue;
                }

                var payDate = inv.DueDate.AddDays(-Rng.Next(1, 10));
                var gateway = Rng.Next(3) switch { 0 => PaymentMethodNames.Card, 1 => PaymentMethodNames.PayPal, _ => PaymentMethodNames.BankTransfer };
                var fees = Math.Round(inv.Total * 0.029m + 0.30m, 2);

                var tx = InvoiceTransaction.Create(
                    inv.Id, payDate, gateway,
                    $"txn_{Rng.Next(100000, 999999)}",
                    inv.Total, InvoiceTransactionType.Payment, fees);

                _db.InvoiceTransactions.Add(tx);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} invoice transactions", paidInvoices.Count);
        }

        // ── Client Transactions ───────────────────────────────────────────────
        if (!await _db.Transactions.AnyAsync(ct))
        {
            var activeClients = clients.Where(c => c.Status == ClientStatus.Active).ToList();

            foreach (var client in activeClients.Take(8))
            {
                for (int m = 0; m < 3; m++)
                {
                    var date = MonthsAgo(_now, m * 2, Rng.Next(1, 28));
                    var amount = Math.Round((decimal)(Rng.NextDouble() * 200 + 50), 2);
                    var tx = Transaction.Create(
                        client.Id, date, "Monthly hosting payment",
                        "txn_" + Rng.Next(100000, 999999), null, PaymentMethodNames.Card,
                        amount, 0m, Math.Round(amount * 0.029m, 2), false, SeedInvoiceCurrency,
                        TransactionSource.Manual);
                    _db.Transactions.Add(tx);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded client transactions");
        }

        // ── Quotes ────────────────────────────────────────────────────────────
        if (!await _db.Quotes.AnyAsync(ct))
        {
            string[] quoteSubjects = [
                "Enterprise Hosting Package", "Custom VPS Solution", "Annual Hosting Plan",
                "E-commerce Hosting Bundle", "Managed WordPress Hosting", "Cloud Migration Package"
            ];

            foreach (var client in clients.Take(8))
            {
                var subject = quoteSubjects[Rng.Next(quoteSubjects.Length)];
                var expiryDate = _now.AddDays(30 + Rng.Next(30));
                var quote = Quote.Create(
                    client.Id, subject, expiryDate, SeedInvoiceCurrency, "Please review and accept this quote.");
                quote.AddItem("Setup Fee", 99.00m, 1);
                quote.AddItem("Monthly Hosting (12 months)", 29.99m, 12);
                if (Rng.Next(2) == 0)
                {
                    quote.AddItem("SSL Certificate", 14.99m, 1);
                }

                _db.Quotes.Add(quote);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded quotes");
        }

        // ── Billable Items ────────────────────────────────────────────────────
        if (!await _db.BillableItems.AnyAsync(ct))
        {
            string[] itemDescs = ["Extra Bandwidth", "Additional Storage", "Premium Support", "Setup Fee", "Custom Development"];
            decimal[] itemPrices = [5.00m, 10.00m, 25.00m, 50.00m, 150.00m];

            foreach (var client in clients.Take(6))
            {
                var idx = Rng.Next(itemDescs.Length);
                var item = BillableItem.Create(
                    client.Id, itemDescs[idx], itemPrices[idx], SeedInvoiceCurrency,
                    BillableItemType.OneTime, null, _now);
                _db.BillableItems.Add(item);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded billable items");
        }

        // ── Product Groups + Products ─────────────────────────────────────────
        if (!await _db.ProductGroups.AnyAsync(ct))
        {
            var groupDefs = new[]
            {
                ("Web Hosting",       ProductType.SharedHosting, new[] { ("Starter Hosting", 4.99m, 49.99m), ("Business Hosting", 9.99m, 99.99m), ("Pro Hosting", 19.99m, 199.99m) }),
                ("VPS Servers",       ProductType.Vps,           new[] { ("VPS Basic", 29.99m, 299.99m), ("VPS Pro", 59.99m, 599.99m), ("VPS Enterprise", 99.99m, 999.99m) }),
                ("Dedicated Servers", ProductType.Dedicated,     new[] { ("Dedicated Starter", 89.99m, 899.99m), ("Dedicated Pro", 149.99m, 1499.99m) }),
                ("SSL Certificates",  ProductType.Ssl,           new[] { ("SSL Basic", 9.99m, 89.99m), ("SSL Wildcard", 29.99m, 279.99m) }),
                ("Email Hosting",     ProductType.Email,         new[] { ("Email Basic", 2.99m, 29.99m), ("Email Pro", 7.99m, 79.99m) }),
                ("Domains",           ProductType.Domain,        new[] { ("Domain Registration", 0m, 0m) }),
            };

            foreach (var (groupName, productType, products) in groupDefs)
            {
                var group = ProductGroup.Create(groupName, null);
                _db.ProductGroups.Add(group);
                await _db.SaveChangesAsync(ct);

                foreach (var (name, monthly, annual) in products)
                {
                    // A hosting plan names the panel package its accounts are created with; provisioning refuses one without it.
                    var packageName = productType is ProductType.SharedHosting or ProductType.Vps or ProductType.Dedicated
                        ? name.ToLowerInvariant().Replace(' ', '-')
                        : null;
                    // The legacy columns receive the same figures for the rollback release; the
                    // storefront and the order flow read the per-currency rows.
                    var product = Product.Create(group.Id, name, null, null, null, packageName, productType, monthly, annual, _now);
                    product.SetPrice(SeedInvoiceCurrency, BillingCycle.Monthly, monthly);
                    product.SetPrice(SeedInvoiceCurrency, BillingCycle.Annual, annual);
                    _db.Products.Add(product);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded product groups and products");
        }

        // ── Client Services ───────────────────────────────────────────────────
        if (!await _db.ClientServices.AnyAsync(ct))
        {
            var products = await _db.Products.Include(p => p.Prices).ToListAsync(ct);
            var activeClients = await _db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
            var billingCycles = new[] { BillingCycle.Monthly, BillingCycle.Annual };
            var renewalDevPlaced = false;

            foreach (var client in activeClients)
            {
                var numServices = Rng.Next(1, 4);
                for (int i = 0; i < numServices; i++)
                {
                    var product = products[Rng.Next(products.Count)];
                    var cycle = billingCycles[Rng.Next(2)];

                    // Seeded through the ordering factory with both amounts set: every renewal is
                    // billed from RecurringAmount by ProcessRenewalsCronHandler, and without it the
                    // seeded portal shows a wall of 0.00 invoices, so the Pay screens have nothing to
                    // charge and no dev sees what a real balance looks like.
                    var cyclePrice = product.PriceFor(SeedInvoiceCurrency, cycle) ?? 0m;
                    var svc = ClientService.CreateOrdered(
                        client.Id, product.Id, cycle, domain: null,
                        firstPaymentAmount: cyclePrice, recurringAmount: cyclePrice, paymentMethod: null, firstInvoiceId: null, now: _now);

                    var monthsBack = Rng.Next(1, 12);
                    svc.Backdate(MonthsAgo(_now, monthsBack));

                    // Leave the first of a client's several services Pending, the rest Active. This
                    // is what gives the portal a service to run the setup wizard against: without
                    // it every seeded service is already provisioned and "Build My Server" has
                    // nothing to act on. A client with only one service keeps it Active so no
                    // account is left unusable; a Pending service is never activated and holds no
                    // provisioning ref, exactly the state the wizard fills in.
                    if (i == 0 && numServices > 1)
                    {
                        _logger.LogInformation(
                            "Left one service for client {ClientId} Pending to seed the setup flow",
                            client.Id);
                    }
                    else
                    {
                        svc.Activate("prov_" + Rng.Next(10000, 99999), svc.CreatedAt, _now);

                        // One service always renews ten days out, inside the 14-day invoicing window, so
                        // the renewal flow has something to raise an invoice for on a fresh dev database.
                        if (!renewalDevPlaced)
                        {
                            svc.ExtendRenewalTo(_now.AddDays(10));
                            renewalDevPlaced = true;
                        }
                    }

                    _db.ClientServices.Add(svc);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded client services");
        }

        // ── Product-matched invoices ──────────────────────────────────────────
        if (!hasProductInvoices)
        {
            var products = await _db.Products.Include(p => p.Prices).ToListAsync(ct);
            var activeClients = await _db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);

            for (int monthsBack = 2; monthsBack >= 0; monthsBack--)
            {
                var invoicesThisMonth = Rng.Next(5, 12);
                for (int i = 0; i < invoicesThisMonth; i++)
                {
                    var client = activeClients[Rng.Next(activeClients.Count)];
                    var product = products[Rng.Next(products.Count)];
                    var invoiceDate = MonthsAgo(_now, monthsBack, Rng.Next(1, 25));

                    var invoice = Invoice.Create(client.Id, invoiceDate.AddDays(30), SeedInvoiceCurrency, invoiceDate);
                    invoice.AddItem(product.Name, product.PriceFor(SeedInvoiceCurrency, BillingCycle.Monthly) ?? 0m, 1);
                    if (Rng.Next(3) == 0)
                    {
                        var p2 = products[Rng.Next(products.Count)];
                        invoice.AddItem(p2.Name, p2.PriceFor(SeedInvoiceCurrency, BillingCycle.Monthly) ?? 0m, 1);
                    }
                    invoice.RecordPaidHistory("txn_" + Rng.Next(100000, 999999), _now);

                    _db.Invoices.Add(invoice);
                    invoice.Backdate(invoiceDate);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded product-matched invoices for Income by Product report");
        }

        // ── Orders ────────────────────────────────────────────────────────────
        if (!await _db.Orders.AnyAsync(ct))
        {
            var products = await _db.Products.Include(p => p.Prices).ToListAsync(ct);
            var activeClients = await _db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
            var gateways = new[] { PaymentMethodNames.Card, PaymentMethodNames.PayPal, PaymentMethodNames.BankTransfer };

            for (int month = 11; month >= 0; month--)
            {
                var ordersThisMonth = Rng.Next(3, 8);
                for (int o = 0; o < ordersThisMonth; o++)
                {
                    var client = activeClients[Rng.Next(activeClients.Count)];
                    var product = products[Rng.Next(products.Count)];
                    var gateway = gateways[Rng.Next(gateways.Length)];
                    var orderDate = MonthsAgo(_now, month, Rng.Next(1, 28));

                    var order = Order.Create(
                        $"ORD-{Rng.Next(10000, 99999)}", client.Id, gateway, null, SeedInvoiceCurrency, _now);
                    var monthlyPrice = product.PriceFor(SeedInvoiceCurrency, BillingCycle.Monthly) ?? 0m;
                    order.AddItem(product.Id, product.Name, BillingCycle.Monthly, monthlyPrice, monthlyPrice, null, null);
                    order.Accept(_now);

                    _db.Orders.Add(order);
                    order.Backdate(orderDate);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded orders across 12 months");
        }

        // ── SSL checks ────────────────────────────────────────────────────────
        if (!await _db.SslChecks.AnyAsync(ct))
        {
            var now = _now;
            (string domain, bool hasSsl, string? issuer, DateTimeOffset? expires, bool isActive)[] sslDefs =
            [
                ("example.test",         true,  "Example Root CA",       now.AddDays(60),  true),
                ("shop.example",         true,  "Example Issuing CA",    now.AddDays(75),  true),
                ("code.example",         true,  "Sample Trust Services", now.AddDays(45),  true),
                ("cdn.example",          true,  "Example Issuing CA",    now.AddDays(90),  true),
                ("app.example",          true,  "Sample Trust Services", now.AddDays(200), true),
                ("certs.test",           true,  "Example Root CA",       now.AddDays(20),  true),
                ("expired-demo.test",    false, null,                    null,             false),
                ("no-ssl.test",          false, null,                    null,             true),
            ];

            foreach (var (domain, hasSsl, issuer, expires, isActive) in sslDefs)
            {
                _db.SslChecks.Add(SslCheck.Create(domain, hasSsl, issuer, expires, isActive, now));
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded SSL checks");
        }

        // ── Slides ────────────────────────────────────────────────────────────
        if (!await _db.Slides.AnyAsync(ct))
        {
            await SeedSlidesAsync(_db, _now, _logger, ct);
        }

        // ── Support: Tickets, KB ─────────────────────────────────────────────
        // Keyed on tickets, not departments: departments exist in every environment
        // before this runs, so a departments check would skip the KB and tickets too.
        if (!await _db.Tickets.AnyAsync(ct))
        {
            await SeedSupportAsync(_db, _now, ct, _logger);
        }

        // ── Domains ───────────────────────────────────────────────────────────
        if (!await _db.Domains.AnyAsync(ct))
        {
            await SeedDomainsAsync(_db, _now, ct, _logger);
        }

        // ── Servers ───────────────────────────────────────────────────────────
        if (!await _db.ServerGroups.AnyAsync(ct))
        {
            await SeedServersAsync(_db, _now, ct, _logger);
        }

        // ── Settings ──────────────────────────────────────────────────────────
        if (!await _db.Settings.AnyAsync(ct))
        {
            await SeedSettingsAsync(_db, ct, _logger);
        }

        // ── Announcements ─────────────────────────────────────────────────────
        if (!await _db.Announcements.AnyAsync(ct))
        {
            await SeedAnnouncementsAsync(_db, ct, _logger);
        }

        _logger.LogInformation("Dev seed complete");
    }

    // ── Support ───────────────────────────────────────────────────────────────

    /// <summary>Seeds support departments, tickets and replies.</summary>
    /// <param name="db">Context the rows are added to and saved through.</param>
    /// <param name="now">The moment the ticket dates count from.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private static async Task SeedSupportAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct, ILogger logger)
    {
        // Departments are seeded for every environment by DefaultDepartmentsSeeder, which
        // has already run by the time we get here, so take those rather than adding a
        // duplicate set. The tickets below index into this list, so it must be ordered.
        var departments = await db.Departments.OrderBy(d => d.Id).ToListAsync(ct);

        // KB Categories
        var kbCatDefs = new[]
        {
            ("Getting Started",   "Guides to help you get up and running quickly."),
            ("Billing & Payments","Information about invoices, payment methods, and refunds."),
            ("Domains",           "Domain registration, DNS management, and transfers."),
            ("Hosting",           "Shared hosting, VPS, and server management guides."),
            ("Security",          "SSL certificates, 2FA, and account security tips."),
        };

        var kbCategories = new List<KbCategory>();
        foreach (var (name, desc) in kbCatDefs)
        {
            var cat = KbCategory.Create(name, desc, false);
            db.KbCategories.Add(cat);
            kbCategories.Add(cat);
        }
        await db.SaveChangesAsync(ct);

        // KB Articles
        (string Title, string Content, string Category)[] articleDefs =
        [
            ("How to log in to your client portal",
             "Visit the client portal at your panel URL and enter your email and password. If you have 2FA enabled, you will also be prompted for your authenticator code.",
             "Getting Started"),
            ("How to enable Two-Factor Authentication",
             "Go to your Account page, find the Security section, and click Enable 2FA. Scan the QR code with an authenticator app such as Google Authenticator or Authy, then enter the 6-digit code to confirm.",
             "Getting Started"),
            ("How to pay an invoice",
             "Open the Billing section from the navigation menu, select the invoice you wish to pay, and click Pay Now. We accept credit/debit cards and bank transfers.",
             "Billing & Payments"),
            ("Understanding your invoice",
             "Each invoice lists the services you are being billed for, the due date, and the total amount. Invoices marked Overdue have passed their due date without payment.",
             "Billing & Payments"),
            ("How to register a domain",
             "Go to the Domains section and click Register Domain. Search for your desired domain name, select it, and complete the registration form. Domains are activated within minutes.",
             "Domains"),
            ("How to update your DNS records",
             "Open the domain you want to manage, click DNS Management, and use the Add Record form to create A, CNAME, MX, or TXT entries. Changes propagate within 24–48 hours.",
             "Domains"),
            ("Choosing a hosting plan",
             "Starter Hosting is ideal for small websites. Business Hosting suits growing businesses that need more resources. Pro Hosting is recommended for high-traffic sites and developers.",
             "Hosting"),
            ("How to upgrade your hosting plan",
             "Contact our support team via a ticket or open the Services section to request an upgrade. Upgrades are applied within one business day.",
             "Hosting"),
            ("How SSL certificates protect your site",
             "An SSL certificate encrypts the connection between your visitors and your server, protecting sensitive data and improving trust. All plans include a free Let's Encrypt SSL certificate.",
             "Security"),
            ("Keeping your account secure",
             "Use a strong, unique password and enable Two-Factor Authentication. Never share your login credentials. If you suspect unauthorized access, change your password immediately and contact support.",
             "Security"),
        ];

        foreach (var (title, content, category) in articleDefs)
        {
            var article = KbArticle.Create(title, content, category);
            article.Publish();
            db.KbArticles.Add(article);
        }
        await db.SaveChangesAsync(ct);

        // Tickets
        var clients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
        if (clients.Count == 0 || departments.Count == 0)
        {
            logger.LogInformation("Skipping ticket seeding — no active clients or departments");
            return;
        }

        (string Subject, string Message, TicketPriority Priority, int DeptIndex, bool HasReply, bool IsClosed)[] ticketDefs =
        [
            ("Unable to access my control panel", "I have been trying to log into cPanel for the past hour but I keep getting an invalid password error. I have already reset my password but still cannot log in.", TicketPriority.High, 0, true, false),
            ("Invoice #1042 shows incorrect amount", "My latest invoice shows a charge for Dedicated Server Pro but I am subscribed to Business Hosting. Please review and correct this.", TicketPriority.Medium, 1, true, true),
            ("Request to upgrade to VPS Pro", "I would like to upgrade my current Starter Hosting plan to VPS Pro. Could you provide the steps and confirm the new monthly pricing?", TicketPriority.Low, 2, true, false),
            ("Website loading very slowly", "My website has been loading extremely slowly since yesterday. I have not made any changes on my end. Please investigate.", TicketPriority.High, 0, true, false),
            ("Domain transfer status", "I submitted a domain transfer request three days ago and have not received any confirmation. Could you provide an update on the status?", TicketPriority.Medium, 0, false, false),
            ("How do I add an email account?", "I would like to set up an email address using my domain. Could you guide me through the process?", TicketPriority.Low, 3, true, true),
            ("SSL certificate not renewing", "My SSL certificate expired yesterday and my website is showing an insecure warning. The auto-renewal was supposed to handle this.", TicketPriority.High, 0, true, false),
            ("Refund request for cancelled service", "I cancelled my VPS Basic service last week but I have not received my refund yet. The billing period still had 18 days remaining.", TicketPriority.Medium, 1, false, false),
            ("Need help with DNS configuration", "I am trying to point my domain to an external service and need to add a CNAME record. I am not sure which values to use.", TicketPriority.Low, 0, true, true),
            ("Account password reset not working", "I am not receiving the password reset email. I have checked my spam folder and nothing is there. Please assist.", TicketPriority.Medium, 3, true, false),
        ];

        var rng = new Random(42);
        for (var i = 0; i < ticketDefs.Length; i++)
        {
            var (subject, message, priority, deptIndex, hasReply, isClosed) = ticketDefs[i];
            var client = clients[i % clients.Count];
            var dept = departments[deptIndex % departments.Count];

            var ticket = Ticket.Create(client.Id, subject, message, dept.Id, priority, now);
            var openedAt = MonthsAgo(now, rng.Next(0, 3), rng.Next(1, 28));

            if (hasReply)
            {
                ticket.AddReply("Thank you for contacting us. We are looking into this and will get back to you shortly.", "Support Team", true, now);
            }

            if (isClosed)
            {
                ticket.Close(now);
            }

            // After the replies, so the staff answer is moved into the past with the ticket it belongs to.
            ticket.Backdate(openedAt, TimeSpan.FromHours(4));
            db.Tickets.Add(ticket);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded departments, KB categories, KB articles, and tickets");
    }

    // ── Domains ───────────────────────────────────────────────────────────────

    /// <summary>Seeds domains with their DNS records.</summary>
    /// <param name="db">Context the rows are added to and saved through.</param>
    /// <param name="now">The moment the domain dates count from.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private static async Task SeedDomainsAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct, ILogger logger)
    {
        var clients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
        if (clients.Count == 0)
        {
            return;
        }

        (string Name, decimal Price, int ExpiresInDays, bool AutoRenew, bool WhoisPrivacy, string Registrar)[] domainDefs =
        [
            ("example.test",        12.99m, 365,  true,  true,  "Namecheap"),
            ("ownerbusiness.com",    12.99m, 180,  true,  false, "Namecheap"),
            ("customershop.net",     11.99m, 90,   true,  true,  "Name.am"),
            ("developerlab.io",      39.99m, 270,  false, false, "Namecheap"),
            ("managerhub.com",       12.99m, 400,  true,  true,  "Namecheap"),
            ("partnersolutions.org", 10.99m,  10,  true,  false, "Name.am"),
            ("resellerpro.com",      12.99m, 500,  true,  true,  "Namecheap"),
            ("ownersite.am",          8.99m, 60,   false, false, "Name.am"),
        ];

        for (var i = 0; i < domainDefs.Length; i++)
        {
            var (name, price, expiresInDays, autoRenew, whoisPrivacy, registrar) = domainDefs[i];
            var client = clients[i % clients.Count];
            var expiresAt = now.AddDays(expiresInDays);

            var domain = RegisteredDomain.Register(client.Id, name, now, expiresAt, autoRenew, whoisPrivacy,
                SeedInvoiceCurrency, price, price, registrar, 1);

            domain.Activate("REG-" + (100000 + i), domain.ExpiresAt, now);
            domain.SetLock(true);
            domain.SetNameservers(["ns1.example.test", "ns2.example.test"]);

            db.Domains.Add(domain);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} domains", domainDefs.Length);
    }

    // ── Servers ───────────────────────────────────────────────────────────────

    /// <summary>Seeds server groups and servers.</summary>
    /// <param name="db">Context the rows are added to and saved through.</param>
    /// <param name="now">The moment the servers are added and tested at.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private static async Task SeedServersAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct, ILogger logger)
    {
        // Server Groups
        var sharedGroup = ServerGroup.Create("Shared Hosting Cluster", ServerFillType.LeastFull);
        var vpsGroup = ServerGroup.Create("VPS Cluster", ServerFillType.FillUntilFull);
        db.ServerGroups.Add(sharedGroup);
        db.ServerGroups.Add(vpsGroup);
        await db.SaveChangesAsync(ct);

        // Servers
        var serverDefs = new[]
        {
            new ServerDetails("Shared US-01", "shared01.example.test", "192.168.1.10", null,
                ServerModule.CPanel, "root", new ServerCredentials("changeme", null, null),
                true, 500, true, false, 150m, "US-East DC", null,
                new ServerNameServers(
                    "ns1.example.test", "192.168.1.1", "ns2.example.test", "192.168.1.2", null, null, null, null, null, null)),

            new ServerDetails("Shared EU-01", "shared01-eu.example.test", "10.0.0.10", null,
                ServerModule.CPanel, "root", new ServerCredentials("changeme", null, null),
                true, 500, false, false, 150m, "EU-West DC", null,
                new ServerNameServers(
                    "ns1.example.test", "192.168.1.1", "ns2.example.test", "192.168.1.2", null, null, null, null, null, null)),

            new ServerDetails("VPS US-01", "vps01.example.test", "172.16.0.10", null,
                ServerModule.Plesk, "admin", new ServerCredentials("changeme", null, null),
                true, 100, true, false, 350m, "US-East DC", null,
                new ServerNameServers(
                    "ns1.example.test", "192.168.1.1", "ns2.example.test", "192.168.1.2", null, null, null, null, null, null)),

            new ServerDetails("Dedicated US-01", "dedicated01.example.test", "203.0.113.10", null,
                ServerModule.DirectAdmin, "admin", new ServerCredentials("changeme", null, null),
                true, 50, false, false, 800m, "US-West DC", null,
                new ServerNameServers(
                    "ns1.example.test", "192.168.1.1", "ns2.example.test", "192.168.1.2", null, null, null, null, null, null)),

            // The only Cwp7-module server, and the only one the setup flow can actually pick: every
            // hosting product maps to the Cwp7 module (SetupServiceHandler.MapProductTypeToModule),
            // so without a Cwp7 server the server selector returns nothing and "Build My Server"
            // fails before it starts. The access hash is a dev placeholder: the real panel call is
            // short-circuited by the fake provider (Provisioning:UseFakeProvider), so it is never used.
            new ServerDetails("CWP7 US-01", "cwp01.example.test", "203.0.113.20", null,
                ServerModule.Cwp7, "root", new ServerCredentials("changeme", null, "dev-access-hash"),
                true, 200, true, false, 300m, "US-East DC", null,
                new ServerNameServers(
                    "ns1.example.test", "192.168.1.1", "ns2.example.test", "192.168.1.2", null, null, null, null, null, null)),
        };

        var groupIds = new[] { sharedGroup.Id, sharedGroup.Id, vpsGroup.Id, vpsGroup.Id, sharedGroup.Id };
        for (var i = 0; i < serverDefs.Length; i++)
        {
            var server = Server.Create(serverDefs[i], now);
            server.AssignToGroup(groupIds[i]);
            server.RecordConnectionTest(true, 10 + i * 5, now);
            db.Servers.Add(server);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded 2 server groups and {Count} servers", serverDefs.Length);
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    /// <summary>Seeds portal settings.</summary>
    /// <param name="db">Context the rows are added to and saved through.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private static async Task SeedSettingsAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        (string Key, string Value, string Description)[] settingDefs =
        [
            ("company.name",              "HostPanel",                       "Company display name"),
            ("company.email",             "hello@example.test",             "Primary contact email"),
            ("company.phone",             "+1-555-0100",                "Primary support phone number"),
            ("company.address",           "1 Market Street, Fairhaven", "Company mailing address"),
            ("company.logo",              "/images/logo.png",                "Path to company logo"),
            ("billing.currency",          SeedInvoiceCurrency,                           "Default billing currency"),
            ("billing.tax_rate",          "0",                               "Default tax rate percentage"),
            ("billing.payment_gateway",   "Stripe",                        "Default payment gateway"),
            ("support.ticket_auto_close", "14",                              "Days after last reply before a ticket is auto-closed"),
            ("support.department_default","1",                               "Default department ID for new tickets"),
            ("domain.registrar_default",  "Namecheap",                       "Default domain registrar module"),
            ("domain.ns1",                "ns1.example.test",               "Primary nameserver"),
            ("domain.ns2",                "ns2.example.test",               "Secondary nameserver"),
            ("mail.from_address",         "noreply@example.test",           "From address used for outgoing emails"),
            ("mail.from_name",            "HostPanel",                       "From name used for outgoing emails"),
        ];

        foreach (var (key, value, description) in settingDefs)
        {
            db.Settings.Add(Setting.Create(key, value, description));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} system settings", settingDefs.Length);
    }

    // ── Announcements ─────────────────────────────────────────────────────────

    /// <summary>Seeds announcements.</summary>
    /// <param name="db">Context the rows are added to and saved through.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private static async Task SeedAnnouncementsAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        (string Title, string Content, bool IsPublished)[] announcementDefs =
        [
            ("Welcome to HostPanel",
             "We are excited to have you on board! HostPanel gives you everything you need to manage your hosting, domains, billing, and support in one place. If you have any questions, our support team is here to help 24/7.",
             true),
            ("Scheduled Maintenance — June 15",
             "We will be performing scheduled maintenance on our shared hosting servers on June 15 between 02:00 and 04:00 UTC. During this window, websites may experience brief downtime. We apologise for any inconvenience.",
             true),
            ("New Feature: Two-Factor Authentication",
             "We have added Two-Factor Authentication (2FA) to the client portal. We strongly recommend enabling it for added account security. Visit your Account page to set it up in under a minute.",
             true),
        ];

        foreach (var (title, content, isPublished) in announcementDefs)
        {
            db.Announcements.Add(Announcement.Create(title, content, isPublished));
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} announcements", announcementDefs.Length);
    }

    // ── Slides ────────────────────────────────────────────────────────────────

    /// <summary>Seeds the initial homepage slider slides with English translations.</summary>
    /// <param name="db">Context the slides are added to and saved through.</param>
    /// <param name="now">The moment the slides are added at.</param>
    /// <param name="logger">Receives the seeding progress line.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>A task that completes once the slides are saved.</returns>
    private static async Task SeedSlidesAsync(AppDbContext db, DateTimeOffset now, ILogger logger, CancellationToken ct)
    {
        (string Key, string Icon, string Color, string Image, string? DemoUrl, string LearnMoreUrl,
         string Title, string Tagline, string Description, string[] Features, string CtaText)[] slideDefs =
        [
            ("web-hosting", "cloud-check", "#0ea5e9", DemoContentSeeder.SlideImageUrl("web-hosting"),
                null, "/hosting",
                "Web Hosting", "Fast & Reliable Hosting",
                "Launch your website on high-performance servers with free SSL, daily backups and one-click app installs.",
                ["Free SSL certificate", "One-click app installs", "24/7 expert support", "Daily backups"],
                "Get Started"),
            ("domains", "earth", "#3b82f6", DemoContentSeeder.SlideImageUrl("domains"),
                null, "/domains",
                "Domain Registration", "Find & Register Your Domain",
                "Search and register your perfect domain name with competitive pricing and instant activation.",
                ["Free DNS management", "WHOIS privacy protection", "Auto-renewal options"],
                "Get Started"),
            ("cloud-vps", "server", "#8b5cf6", DemoContentSeeder.SlideImageUrl("cloud-vps"),
                null, "/products/cloud-vps",
                "Cloud VPS", "Full Control, Instant Setup",
                "Isolated virtual servers with root access and NVMe storage, ready within minutes of your order.",
                ["Root access", "NVMe storage", "Snapshots included"],
                "Learn More"),
        ];

        for (var sortOrder = 0; sortOrder < slideDefs.Length; sortOrder++)
        {
            var (key, icon, color, image, demoUrl, learnMoreUrl, title, tagline, description, features, ctaText) = slideDefs[sortOrder];
            var slide = Slide.Create(new SlideSettings(icon, color, image, demoUrl, learnMoreUrl, null, sortOrder, true, SlideAudience.All, null, null), now);
            slide.SetTranslation("en", title, tagline, description, features, ctaText, learnMoreUrl);
            db.Slides.Add(slide);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} slides with translations", slideDefs.Length);
    }
}
