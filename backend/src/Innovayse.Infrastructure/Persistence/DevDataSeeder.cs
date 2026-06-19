namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Auth;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Ssl;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Products;
using Innovayse.Domain.Servers;
using Innovayse.Domain.Services;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Support;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Seeds realistic test data for development. Idempotent — skips if clients already exist.
/// </summary>
public sealed class DevDataSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ILogger<DevDataSeeder> logger)
{
    private static readonly Random Rng = new(42);

    private static DateTimeOffset MonthsAgo(int months, int day = 15) =>
        new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero)
            .AddMonths(-months)
            .AddDays(day - 1);

    /// <summary>Seeds test clients, invoices, transactions, quotes and billable items.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ── Admin user ───────────────────────────────────────────────────────
        const string adminEmail = "admin@hostpanel.com";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
            };
            var adminResult = await userManager.CreateAsync(adminUser, "Admin123!");
            if (adminResult.Succeeded)
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }

        var productNames = await db.Products.Select(p => p.Name).ToListAsync(ct);
        var hasProductInvoices = productNames.Count > 0 && await db.InvoiceItems
            .AnyAsync(ii => productNames.Contains(ii.Description), ct);

        if (await db.Clients.AnyAsync(ct) && await db.Invoices.AnyAsync(ct) && await db.Quotes.AnyAsync(ct)
            && await db.ProductGroups.AnyAsync(ct) && await db.Orders.AnyAsync(ct) && hasProductInvoices)
        {
            if (!await db.Slides.AnyAsync(ct))
                await SeedSlidesAsync(db, logger, ct);

            if (!await db.Departments.AnyAsync(ct))
                await SeedSupportAsync(db, ct, logger);

            if (!await db.Domains.AnyAsync(ct))
                await SeedDomainsAsync(db, ct, logger);

            if (!await db.ServerGroups.AnyAsync(ct))
                await SeedServersAsync(db, ct, logger);

            if (!await db.Settings.AnyAsync(ct))
                await SeedSettingsAsync(db, ct, logger);

            if (!await db.Announcements.AnyAsync(ct))
                await SeedAnnouncementsAsync(db, ct, logger);

            logger.LogInformation("Dev seed skipped — data already exists");
            return;
        }

        logger.LogInformation("Seeding dev test data…");

        // ── Clients ──────────────────────────────────────────────────────────
        var clientDefs = new List<(string Email, string First, string Last, string? Company, string? Phone, string? Street, string? City, string? State, string? PostCode, string? Country, ClientStatus Status)>
        {
            ("owner@hostpanel.com",     "Owner",     "One",     "Owner Enterprises",    "+1-555-0101", "123 Main St",        "New York",      "NY",  "10001",    "US", ClientStatus.Active),
            ("owner2@hostpanel.com",    "Owner",     "Two",     "Owner & Co",           "+1-555-0102", "456 Oak Ave",        "San Francisco", "CA",  "94102",    "US", ClientStatus.Active),
            ("owner3@hostpanel.com",    "Owner",     "Three",   "DigiTech Solutions",   "+374-91-123456", "15 Tumanyan St",  "Yerevan",       null,  "0001",     "AM", ClientStatus.Active),
            ("customer@hostpanel.com",  "Customer",  "One",     null,                   "+34-612-345678", "Calle Mayor 22",  "Madrid",        null,  "28013",    "ES", ClientStatus.Active),
            ("customer2@hostpanel.com", "Customer",  "Two",     "Customer Web Solutions", "+44-20-7946-0958", "10 Downing Rd", "London",     null,  "SW1A 2AA", "GB", ClientStatus.Active),
            ("customer3@hostpanel.com", "Customer",  "Three",   null,                   "+1-555-0106", "789 Pine Ln",        "Chicago",       "IL",  "60601",    "US", ClientStatus.Suspended),
            ("customer4@hostpanel.com", "Customer",  "Four",    "Customer Hosting",     "+1-555-0107", "321 Elm St",         "Austin",        "TX",  "73301",    "US", ClientStatus.Active),
            ("customer5@hostpanel.com", "Customer",  "Five",    "Customer Digital",     "+82-10-1234-5678", "Gangnam-gu",  "Seoul",         null,  "06000",    "KR", ClientStatus.Active),
            ("developer@hostpanel.com", "Developer", "One",     null,                   "+33-6-12-34-56-78", "8 Rue de Rivoli", "Paris",     null,  "75001",    "FR", ClientStatus.Inactive),
            ("developer2@hostpanel.com","Developer", "Two",     "Dev Media",            "+61-2-1234-5678", "42 George St",   "Sydney",        "NSW", "2000",     "AU", ClientStatus.Active),
            ("manager@hostpanel.com",   "Manager",   "One",     null,                   null,          null,                 null,            null,  null,       null, ClientStatus.Active),
            ("manager2@hostpanel.com",  "Manager",   "Two",     "Manager Corp",         "+1-555-0112", "555 Broadway",       "New York",      "NY",  "10012",    "US", ClientStatus.Active),
            ("partner@hostpanel.com",   "Partner",   "One",     null,                   "+49-30-12345678", "Friedrichstr. 100", "Berlin",   null,  "10117",    "DE", ClientStatus.Active),
            ("reseller@hostpanel.com",  "Reseller",  "One",     "Reseller Cloud Hosting", "+1-555-0114", "900 Cloud Dr",   "Seattle",       "WA",  "98101",    "US", ClientStatus.Closed),
            ("reseller2@hostpanel.com", "Reseller",  "Two",     null,                   "+1-555-0115", "77 Sunset Blvd",     "Los Angeles",   "CA",  "90028",    "US", ClientStatus.Active),
            ("customer6@hostpanel.com", "Customer",  "Six",     "Six Tech Ltd",         "+1-416-555-0101", "200 Bay St",     "Toronto",       "ON",  "M5J 2J3",  "CA", ClientStatus.Active),
            ("customer7@hostpanel.com", "Customer",  "Seven",   null,                   "+55-11-91234-5678", "Av. Paulista 1000", "São Paulo", null, "01310-100", "BR", ClientStatus.Active),
            ("developer3@hostpanel.com","Developer", "Three",   "Dev Studio AM",        "+374-55-987654", "20 Baghramyan", "Yerevan",       null,  "0019",     "AM", ClientStatus.Active),
            ("partner2@hostpanel.com",  "Partner",   "Two",     "Partner Solutions",    "+971-50-123-4567", "Sheikh Zayed Rd", "Dubai",      null,  "00000",    "AE", ClientStatus.Active),
            ("reseller3@hostpanel.com", "Reseller",  "Three",   "Reseller Networks",    "+65-9123-4567", "1 Raffles Place",  "Singapore",     null,  "048616",   "SG", ClientStatus.Suspended),
        };

        if (!await db.Clients.AnyAsync(ct))
        {
            var clientIdx = 0;
            foreach (var (email, first, last, company, phone, street, city, state, postCode, country, status) in clientDefs)
            {
                var user = new AppUser { UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
                var result = await userManager.CreateAsync(user, "Test@12345");
                if (!result.Succeeded) continue;
                await userManager.AddToRoleAsync(user, Roles.Client);

                var client = Client.Create(user.Id, first, last, email, company);
                if (phone is not null) client.Update(first, last, company, phone);
                if (street is not null) client.UpdateAddress(street, null, city, state, postCode, country);
                if (status == ClientStatus.Suspended) client.Suspend();

                db.Clients.Add(client);

                var signupMonthsBack = 5 - (clientIdx * 6 / clientDefs.Count);
                db.Entry(client).Property(nameof(Client.CreatedAt)).CurrentValue = MonthsAgo(signupMonthsBack, Rng.Next(1, 28));
                clientIdx++;

                await db.SaveChangesAsync(ct);

                if (company is not null)
                {
                    client.AddContact(first, last, company, email, phone, ContactType.Billing, null, null, null, null, null, null, true, true, true, true, true, true);
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        var clients = await db.Clients.ToListAsync(ct);

        // ── Invoices spread across 6 months ─────────────────────────────────
        if (!await db.Quotes.AnyAsync(ct))
        {
            var invoices = new List<Invoice>();

            string[] products = ["Shared Hosting Basic", "Shared Hosting Pro", "VPS Server", "Dedicated Server", "SSL Certificate", "Domain Registration", "Email Hosting", "CDN Service", "Backup Service", "Firewall Service"];
            decimal[] prices = [9.99m, 29.99m, 49.99m, 99.99m, 14.99m, 12.99m, 19.99m, 24.99m, 7.99m, 39.99m];

            foreach (var client in clients)
            {
                for (int monthsBack = 5; monthsBack >= 0; monthsBack--)
                {
                    var invoiceDate = MonthsAgo(monthsBack, Rng.Next(1, 20));
                    var dueDate = invoiceDate.AddDays(30);
                    var productIndex = Rng.Next(products.Length);

                    var invoice = Invoice.Create(client.Id, dueDate);
                    invoice.AddItem(products[productIndex], prices[productIndex], 1);

                    if (Rng.Next(3) == 0)
                    {
                        var idx2 = Rng.Next(products.Length);
                        invoice.AddItem(products[idx2], prices[idx2], 1);
                    }

                    if (monthsBack >= 4)
                        invoice.MarkPaid("stripe_" + Rng.Next(100000, 999999));
                    else if (monthsBack == 3)
                    {
                        if (Rng.Next(2) == 0) invoice.MarkPaid("stripe_" + Rng.Next(100000, 999999));
                        else invoice.MarkOverdue();
                    }
                    else if (monthsBack == 2)
                    {
                        if (Rng.Next(3) == 0) invoice.MarkOverdue();
                    }

                    db.Invoices.Add(invoice);
                    invoices.Add(invoice);
                    db.Entry(invoice).Property(nameof(Invoice.CreatedAt)).CurrentValue = invoiceDate;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} invoices across 6 months", invoices.Count);
        }

        // ── Invoice Transactions ──────────────────────────────────────────────
        if (!await db.InvoiceTransactions.AnyAsync(ct))
        {
            var paidInvoices = await db.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid)
                .ToListAsync(ct);

            foreach (var inv in paidInvoices)
            {
                if (inv.Total <= 0) continue;
                var payDate = inv.DueDate.AddDays(-Rng.Next(1, 10));
                var gateway = Rng.Next(3) switch { 0 => "Stripe", 1 => "PayPal", _ => "Bank Transfer" };
                var fees = Math.Round(inv.Total * 0.029m + 0.30m, 2);

                var tx = InvoiceTransaction.Create(
                    inv.Id, payDate, gateway,
                    $"txn_{Rng.Next(100000, 999999)}",
                    inv.Total, InvoiceTransactionType.Payment, fees);

                db.InvoiceTransactions.Add(tx);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} invoice transactions", paidInvoices.Count);
        }

        // ── Client Transactions ───────────────────────────────────────────────
        if (!await db.Transactions.AnyAsync(ct))
        {
            var activeClients = clients.Where(c => c.Status == ClientStatus.Active).ToList();

            foreach (var client in activeClients.Take(8))
            {
                for (int m = 0; m < 3; m++)
                {
                    var date = MonthsAgo(m * 2, Rng.Next(1, 28));
                    var amount = Math.Round((decimal)(Rng.NextDouble() * 200 + 50), 2);
                    var tx = Transaction.Create(
                        client.Id, date, "Monthly hosting payment",
                        "txn_" + Rng.Next(100000, 999999), null, "Stripe",
                        amount, 0m, Math.Round(amount * 0.029m, 2), false);
                    db.Transactions.Add(tx);
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded client transactions");
        }

        // ── Quotes ────────────────────────────────────────────────────────────
        if (!await db.Quotes.AnyAsync(ct))
        {
            string[] quoteSubjects = [
                "Enterprise Hosting Package", "Custom VPS Solution", "Annual Hosting Plan",
                "E-commerce Hosting Bundle", "Managed WordPress Hosting", "Cloud Migration Package"
            ];

            foreach (var client in clients.Take(8))
            {
                var subject = quoteSubjects[Rng.Next(quoteSubjects.Length)];
                var expiryDate = DateTimeOffset.UtcNow.AddDays(30 + Rng.Next(30));
                var quote = Quote.Create(client.Id, subject, expiryDate, "Please review and accept this quote.");
                quote.AddItem("Setup Fee", 99.00m, 1);
                quote.AddItem("Monthly Hosting (12 months)", 29.99m, 12);
                if (Rng.Next(2) == 0)
                    quote.AddItem("SSL Certificate", 14.99m, 1);
                db.Quotes.Add(quote);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded quotes");
        }

        // ── Billable Items ────────────────────────────────────────────────────
        if (!await db.BillableItems.AnyAsync(ct))
        {
            string[] itemDescs = ["Extra Bandwidth", "Additional Storage", "Premium Support", "Setup Fee", "Custom Development"];
            decimal[] itemPrices = [5.00m, 10.00m, 25.00m, 50.00m, 150.00m];

            foreach (var client in clients.Take(6))
            {
                var idx = Rng.Next(itemDescs.Length);
                var item = BillableItem.Create(
                    client.Id, itemDescs[idx], itemPrices[idx], "USD",
                    BillableItemType.OneTime, null, DateTimeOffset.UtcNow);
                db.BillableItems.Add(item);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded billable items");
        }

        // ── Product Groups + Products ─────────────────────────────────────────
        if (!await db.ProductGroups.AnyAsync(ct))
        {
            var groupDefs = new[]
            {
                ("Web Hosting",       ProductType.SharedHosting, new[] { ("Starter Hosting", 4.99m, 49.99m), ("Business Hosting", 9.99m, 99.99m), ("Pro Hosting", 19.99m, 199.99m) }),
                ("VPS Servers",       ProductType.Vps,           new[] { ("VPS Basic", 29.99m, 299.99m), ("VPS Pro", 59.99m, 599.99m), ("VPS Enterprise", 99.99m, 999.99m) }),
                ("Dedicated Servers", ProductType.Dedicated,     new[] { ("Dedicated Starter", 89.99m, 899.99m), ("Dedicated Pro", 149.99m, 1499.99m) }),
                ("SSL Certificates",  ProductType.Ssl,           new[] { ("SSL Basic", 9.99m, 89.99m), ("SSL Wildcard", 29.99m, 279.99m) }),
                ("Email Hosting",     ProductType.Other,         new[] { ("Email Basic", 2.99m, 29.99m), ("Email Pro", 7.99m, 79.99m) }),
            };

            foreach (var (groupName, productType, products) in groupDefs)
            {
                var group = ProductGroup.Create(groupName, null);
                db.ProductGroups.Add(group);
                await db.SaveChangesAsync(ct);

                foreach (var (name, monthly, annual) in products)
                {
                    var product = Product.Create(group.Id, name, null, null, null, null, productType, monthly, annual);
                    db.Products.Add(product);
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded product groups and products");
        }

        // ── Client Services ───────────────────────────────────────────────────
        if (!await db.ClientServices.AnyAsync(ct))
        {
            var products = await db.Products.ToListAsync(ct);
            var activeClients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
            var billingCycles = new[] { "monthly", "annual" };

            foreach (var client in activeClients)
            {
                var numServices = Rng.Next(1, 4);
                for (int i = 0; i < numServices; i++)
                {
                    var product = products[Rng.Next(products.Count)];
                    var cycle = billingCycles[Rng.Next(2)];
                    var svc = ClientService.Create(client.Id, product.Id, cycle);
                    var monthsBack = Rng.Next(1, 12);
                    db.Entry(svc).Property(nameof(ClientService.CreatedAt)).CurrentValue = MonthsAgo(monthsBack);
                    svc.Activate("prov_" + Rng.Next(10000, 99999));
                    db.ClientServices.Add(svc);
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded client services");
        }

        // ── Product-matched invoices ──────────────────────────────────────────
        if (!hasProductInvoices)
        {
            var products = await db.Products.ToListAsync(ct);
            var activeClients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);

            for (int monthsBack = 2; monthsBack >= 0; monthsBack--)
            {
                var invoicesThisMonth = Rng.Next(5, 12);
                for (int i = 0; i < invoicesThisMonth; i++)
                {
                    var client = activeClients[Rng.Next(activeClients.Count)];
                    var product = products[Rng.Next(products.Count)];
                    var invoiceDate = MonthsAgo(monthsBack, Rng.Next(1, 25));

                    var invoice = Invoice.Create(client.Id, invoiceDate.AddDays(30));
                    invoice.AddItem(product.Name, product.MonthlyPrice, 1);
                    if (Rng.Next(3) == 0)
                    {
                        var p2 = products[Rng.Next(products.Count)];
                        invoice.AddItem(p2.Name, p2.MonthlyPrice, 1);
                    }
                    invoice.MarkPaid("stripe_" + Rng.Next(100000, 999999));

                    db.Invoices.Add(invoice);
                    db.Entry(invoice).Property(nameof(Invoice.CreatedAt)).CurrentValue = invoiceDate;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded product-matched invoices for Income by Product report");
        }

        // ── Orders ────────────────────────────────────────────────────────────
        if (!await db.Orders.AnyAsync(ct))
        {
            var products = await db.Products.ToListAsync(ct);
            var activeClients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
            var gateways = new[] { "Stripe", "PayPal", "Bank Transfer" };

            for (int month = 11; month >= 0; month--)
            {
                var ordersThisMonth = Rng.Next(3, 8);
                for (int o = 0; o < ordersThisMonth; o++)
                {
                    var client = activeClients[Rng.Next(activeClients.Count)];
                    var product = products[Rng.Next(products.Count)];
                    var gateway = gateways[Rng.Next(gateways.Length)];
                    var orderDate = MonthsAgo(month, Rng.Next(1, 28));

                    var order = Order.Create($"ORD-{Rng.Next(10000, 99999)}", client.Id, gateway, null);
                    order.AddItem(product.Id, product.Name, "monthly", product.MonthlyPrice, product.MonthlyPrice, null, null);
                    order.Accept();

                    db.Orders.Add(order);
                    db.Entry(order).Property(nameof(Order.CreatedAt)).CurrentValue = orderDate;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded orders across 12 months");
        }

        // ── SSL checks ────────────────────────────────────────────────────────
        if (!await db.SslChecks.AnyAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            (string domain, bool hasSsl, string? issuer, DateTimeOffset? expires, bool isActive)[] sslDefs =
            [
                ("hostpanel.com",       true,  "Let's Encrypt",         now.AddDays(60),  true),
                ("google.com",          true,  "Google Trust Services", now.AddDays(75),  true),
                ("github.com",          true,  "DigiCert Inc",          now.AddDays(45),  true),
                ("cloudflare.com",      true,  "Google Trust Services", now.AddDays(90),  true),
                ("microsoft.com",       true,  "DigiCert Inc",          now.AddDays(200), true),
                ("letsencrypt.org",     true,  "Let's Encrypt",         now.AddDays(20),  true),
                ("expired-demo.local",  false, null,                    null,             false),
                ("no-ssl.local",        false, null,                    null,             true),
            ];

            foreach (var (domain, hasSsl, issuer, expires, isActive) in sslDefs)
                db.SslChecks.Add(SslCheck.Create(domain, hasSsl, issuer, expires, isActive));

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded SSL checks");
        }

        // ── Slides ────────────────────────────────────────────────────────────
        if (!await db.Slides.AnyAsync(ct))
            await SeedSlidesAsync(db, logger, ct);

        // ── Support: Departments, Tickets, KB ────────────────────────────────
        if (!await db.Departments.AnyAsync(ct))
            await SeedSupportAsync(db, ct, logger);

        // ── Domains ───────────────────────────────────────────────────────────
        if (!await db.Domains.AnyAsync(ct))
            await SeedDomainsAsync(db, ct, logger);

        // ── Servers ───────────────────────────────────────────────────────────
        if (!await db.ServerGroups.AnyAsync(ct))
            await SeedServersAsync(db, ct, logger);

        // ── Settings ──────────────────────────────────────────────────────────
        if (!await db.Settings.AnyAsync(ct))
            await SeedSettingsAsync(db, ct, logger);

        // ── Announcements ─────────────────────────────────────────────────────
        if (!await db.Announcements.AnyAsync(ct))
            await SeedAnnouncementsAsync(db, ct, logger);

        logger.LogInformation("Dev seed complete");
    }

    // ── Support ───────────────────────────────────────────────────────────────

    private static async Task SeedSupportAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        // Departments
        var deptDefs = new[]
        {
            ("Technical Support", "support@hostpanel.com"),
            ("Billing",           "billing@hostpanel.com"),
            ("Sales",             "sales@hostpanel.com"),
            ("General",           "hello@hostpanel.com"),
        };

        var departments = new List<Department>();
        foreach (var (name, email) in deptDefs)
        {
            var dept = Department.Create(name, email);
            db.Departments.Add(dept);
            departments.Add(dept);
        }
        await db.SaveChangesAsync(ct);

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

            var ticket = Ticket.Create(client.Id, subject, message, dept.Id, priority);
            db.Entry(ticket).Property(nameof(Ticket.CreatedAt)).CurrentValue = MonthsAgo(rng.Next(0, 3), rng.Next(1, 28));

            if (hasReply)
                ticket.AddReply("Thank you for contacting us. We are looking into this and will get back to you shortly.", "Support Team", true);

            if (isClosed)
                ticket.Close();

            db.Tickets.Add(ticket);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded departments, KB categories, KB articles, and tickets");
    }

    // ── Domains ───────────────────────────────────────────────────────────────

    private static async Task SeedDomainsAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        var clients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);
        if (clients.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        (string Name, decimal Price, int ExpiresInDays, bool AutoRenew, bool WhoisPrivacy, string Registrar)[] domainDefs =
        [
            ("hostpanel.com",        12.99m, 365,  true,  true,  "Namecheap"),
            ("ownerbusiness.com",    12.99m, 180,  true,  false, "Namecheap"),
            ("customershop.net",     11.99m, 90,   true,  true,  "Name.am"),
            ("developerlab.io",      39.99m, 270,  false, false, "Namecheap"),
            ("managerhub.com",       12.99m, 400,  true,  true,  "Namecheap"),
            ("partnersolutions.org", 10.99m,  25,  true,  false, "Name.am"),
            ("resellerpro.com",      12.99m, 500,  true,  true,  "Namecheap"),
            ("ownersite.am",          8.99m, 60,   false, false, "Name.am"),
        ];

        for (var i = 0; i < domainDefs.Length; i++)
        {
            var (name, price, expiresInDays, autoRenew, whoisPrivacy, registrar) = domainDefs[i];
            var client = clients[i % clients.Count];
            var expiresAt = now.AddDays(expiresInDays);

            var domain = Domain.Register(client.Id, name, expiresAt, autoRenew, whoisPrivacy,
                price, price, "USD", registrar, 1);

            domain.Activate("REG-" + (100000 + i));
            domain.SetLock(true);
            domain.SetNameservers(["ns1.hostpanel.com", "ns2.hostpanel.com"]);

            db.Domains.Add(domain);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} domains", domainDefs.Length);
    }

    // ── Servers ───────────────────────────────────────────────────────────────

    private static async Task SeedServersAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        // Server Groups
        var sharedGroup = ServerGroup.Create("Shared Hosting Cluster", ServerFillType.LeastFull);
        var vpsGroup    = ServerGroup.Create("VPS Cluster", ServerFillType.FillUntilFull);
        db.ServerGroups.Add(sharedGroup);
        db.ServerGroups.Add(vpsGroup);
        await db.SaveChangesAsync(ct);

        // Servers
        var serverDefs = new[]
        {
            new ServerDetails("Shared US-01", "shared01.hostpanel.com", "192.168.1.10", null,
                ServerModule.CPanel, "root", "changeme", null, null,
                true, 500, true, false, 150m, "US-East DC", null,
                "ns1.hostpanel.com", "192.168.1.1", "ns2.hostpanel.com", "192.168.1.2",
                null, null, null, null, null, null),

            new ServerDetails("Shared EU-01", "shared01-eu.hostpanel.com", "10.0.0.10", null,
                ServerModule.CPanel, "root", "changeme", null, null,
                true, 500, false, false, 150m, "EU-West DC", null,
                "ns1.hostpanel.com", "192.168.1.1", "ns2.hostpanel.com", "192.168.1.2",
                null, null, null, null, null, null),

            new ServerDetails("VPS US-01", "vps01.hostpanel.com", "172.16.0.10", null,
                ServerModule.Plesk, "admin", "changeme", null, null,
                true, 100, true, false, 350m, "US-East DC", null,
                "ns1.hostpanel.com", "192.168.1.1", "ns2.hostpanel.com", "192.168.1.2",
                null, null, null, null, null, null),

            new ServerDetails("Dedicated US-01", "dedicated01.hostpanel.com", "203.0.113.10", null,
                ServerModule.DirectAdmin, "admin", "changeme", null, null,
                true, 50, false, false, 800m, "US-West DC", null,
                "ns1.hostpanel.com", "192.168.1.1", "ns2.hostpanel.com", "192.168.1.2",
                null, null, null, null, null, null),
        };

        var groupIds = new[] { sharedGroup.Id, sharedGroup.Id, vpsGroup.Id, vpsGroup.Id };
        for (var i = 0; i < serverDefs.Length; i++)
        {
            var server = Server.Create(serverDefs[i]);
            server.AssignToGroup(groupIds[i]);
            server.RecordConnectionTest(true, 10 + i * 5);
            db.Servers.Add(server);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded 2 server groups and {Count} servers", serverDefs.Length);
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private static async Task SeedSettingsAsync(AppDbContext db, CancellationToken ct, ILogger logger)
    {
        (string Key, string Value, string Description)[] settingDefs =
        [
            ("company.name",              "HostPanel",                       "Company display name"),
            ("company.email",             "hello@hostpanel.com",             "Primary contact email"),
            ("company.phone",             "+1-800-HOSTPANEL",                "Primary support phone number"),
            ("company.address",           "123 Server Street, New York, NY", "Company mailing address"),
            ("company.logo",              "/images/logo.png",                "Path to company logo"),
            ("billing.currency",          "USD",                             "Default billing currency"),
            ("billing.tax_rate",          "0",                               "Default tax rate percentage"),
            ("billing.late_fee",          "5",                               "Late fee percentage applied to overdue invoices"),
            ("billing.payment_gateway",   "Stripe",                          "Default payment gateway"),
            ("support.ticket_auto_close", "14",                              "Days after last reply before a ticket is auto-closed"),
            ("support.department_default","1",                               "Default department ID for new tickets"),
            ("domain.registrar_default",  "Namecheap",                       "Default domain registrar module"),
            ("domain.ns1",                "ns1.hostpanel.com",               "Primary nameserver"),
            ("domain.ns2",                "ns2.hostpanel.com",               "Secondary nameserver"),
            ("mail.from_address",         "noreply@hostpanel.com",           "From address used for outgoing emails"),
            ("mail.from_name",            "HostPanel",                       "From name used for outgoing emails"),
        ];

        foreach (var (key, value, description) in settingDefs)
            db.Settings.Add(Setting.Create(key, value, description));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} system settings", settingDefs.Length);
    }

    // ── Announcements ─────────────────────────────────────────────────────────

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
            db.Announcements.Add(Announcement.Create(title, content, isPublished));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} announcements", announcementDefs.Length);
    }

    // ── Slides ────────────────────────────────────────────────────────────────

    /// <summary>Seeds the initial homepage slider slides with English translations.</summary>
    private static async Task SeedSlidesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        (string Key, string Icon, string Color, string Image, string? DemoUrl, string LearnMoreUrl,
         string Title, string Tagline, string Description, string Features, string CtaText)[] slideDefs =
        [
            ("web-hosting", "cloud-check", "#0ea5e9", "/images/products/hosting.jpg",
                null, "/hosting",
                "Web Hosting", "Fast & Reliable Hosting",
                "Launch your website on high-performance servers with 99.9% uptime SLA, free SSL, and one-click app installs.",
                "[\"Free SSL certificate\",\"99.9% uptime SLA\",\"One-click app installs\",\"24/7 expert support\",\"Daily backups\"]",
                "Get Started"),
            ("domains", "earth", "#3b82f6", "/images/products/domains.jpg",
                null, "/domains",
                "Domain Registration", "Find & Register Your Domain",
                "Search and register your perfect domain name with competitive pricing and instant activation.",
                "[\"Free DNS management\",\"WHOIS privacy protection\",\"Auto-renewal options\"]",
                "Get Started"),
            ("kelpie-ai", "brain", "#7c3aed", "/images/products/kelpie-ai.jpg",
                "https://kelpie-ai.ai", "/products/kelpie-ai",
                "Kelpie AI", "AI-Powered Automation Platform",
                "Harness the power of artificial intelligence to automate workflows, generate insights, and scale your business faster.",
                "[\"Natural language processing\",\"Workflow automation\",\"Real-time analytics\",\"API integrations\",\"Enterprise security\"]",
                "Get Started"),
            ("metricskit-pro", "chart-bar", "#ec4899", "/images/products/metricskit-pro.jpg",
                "#", "/products/metricskit-pro",
                "MetricsKit Pro", "Advanced Analytics & Reporting",
                "Track every metric that matters. Beautiful dashboards, real-time data, and actionable insights for your business.",
                "[\"Real-time dashboards\",\"Custom reports\",\"Multi-source data\",\"Automated alerts\",\"White-label ready\"]",
                "Get Started"),
            ("smartlearn-system", "school", "#8b5cf6", "/images/products/smartlearn-system.jpg",
                "#", "/products/smartlearn-system",
                "SmartLearn System", "Modern E-Learning Platform",
                "Deliver engaging online courses, track learner progress, and grow your educational business with a full-featured LMS.",
                "[\"Course builder\",\"Progress tracking\",\"Certificates & badges\",\"Live sessions\",\"White-label branding\"]",
                "Get Started"),
            ("propsystem-pro", "office-building", "#06b6d4", "/images/products/propsystem-pro.jpg",
                "#", "/products/propsystem-pro",
                "PropSystem Pro", "Complete Property Management",
                "Manage properties, tenants, maintenance requests, and payments all in one platform built for real estate professionals.",
                "[\"Tenant portal\",\"Online rent collection\",\"Maintenance tracking\",\"Financial reporting\",\"Document storage\"]",
                "Get Started"),
            ("shopkit-pro", "cart", "#f59e0b", "/images/products/shopkit-pro.jpg",
                "#", "/products/shopkit-pro",
                "ShopKit Pro", "E-Commerce Made Simple",
                "Launch your online store in minutes with built-in payments, inventory management, and multi-channel selling tools.",
                "[\"Built-in payments\",\"Inventory management\",\"Multi-channel selling\",\"SEO optimized\",\"Mobile storefront\"]",
                "Get Started"),
            ("quickbite", "silverware-fork-knife", "#ef4444", "/images/products/quickbite.jpg",
                "#", "/products/quickbite",
                "QuickBite", "Restaurant & Food Ordering System",
                "Power your restaurant with online ordering, table management, and kitchen display integration — all in one platform.",
                "[\"Online ordering\",\"Table management\",\"Kitchen display\",\"Menu builder\",\"Delivery integration\"]",
                "Get Started"),
            ("taskero", "view-dashboard", "#10b981", "/images/products/taskero.jpg",
                "#", "/products/taskero",
                "Taskero", "Project & Task Management",
                "Organise projects, assign tasks, and collaborate with your team using a clean and intuitive project management platform.",
                "[\"Kanban & list views\",\"Time tracking\",\"Team collaboration\",\"Reporting & insights\",\"Integrations\"]",
                "Get Started"),
        ];

        for (var sortOrder = 0; sortOrder < slideDefs.Length; sortOrder++)
        {
            var (key, icon, color, image, demoUrl, learnMoreUrl, title, tagline, description, features, ctaText) = slideDefs[sortOrder];
            var slide = Slide.Create(icon, color, image, demoUrl, learnMoreUrl, null, sortOrder, true, SlideAudience.All, null, null);
            slide.SetTranslation("en", title, tagline, description, features, ctaText, learnMoreUrl);
            db.Slides.Add(slide);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} slides with translations", slideDefs.Length);
    }
}
