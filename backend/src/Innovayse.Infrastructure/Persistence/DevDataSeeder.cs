namespace Innovayse.Infrastructure.Persistence;

using Innovayse.Domain.Auth;
using Innovayse.Domain.Ssl;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Products;
using Innovayse.Domain.Services;
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
        const string adminEmail = "admin@innovayse.com";
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
            logger.LogInformation("Dev seed skipped — data already exists");
            return;
        }

        logger.LogInformation("Seeding dev test data…");

        // ── Clients ──────────────────────────────────────────────────────────
        var clientDefs = new List<(string Email, string First, string Last, string? Company, string? Phone, string? Street, string? City, string? State, string? PostCode, string? Country, ClientStatus Status)>
        {
            ("john.doe@example.com", "John", "Doe", "Doe Enterprises", "+1-555-0101", "123 Main St", "New York", "NY", "10001", "US", ClientStatus.Active),
            ("jane.smith@example.com", "Jane", "Smith", "Smith & Co", "+1-555-0102", "456 Oak Ave", "San Francisco", "CA", "94102", "US", ClientStatus.Active),
            ("alex.petrosyan@example.com", "Alex", "Petrosyan", "DigiTech AM", "+374-91-123456", "15 Tumanyan St", "Yerevan", null, "0001", "AM", ClientStatus.Active),
            ("maria.garcia@example.com", "Maria", "Garcia", null, "+34-612-345678", "Calle Mayor 22", "Madrid", null, "28013", "ES", ClientStatus.Active),
            ("oliver.brown@example.com", "Oliver", "Brown", "Brown Web Solutions", "+44-20-7946-0958", "10 Downing Rd", "London", null, "SW1A 2AA", "GB", ClientStatus.Active),
            ("emma.wilson@example.com", "Emma", "Wilson", null, "+1-555-0106", "789 Pine Ln", "Chicago", "IL", "60601", "US", ClientStatus.Suspended),
            ("liam.johnson@example.com", "Liam", "Johnson", "Johnson Hosting", "+1-555-0107", "321 Elm St", "Austin", "TX", "73301", "US", ClientStatus.Active),
            ("sophia.lee@example.com", "Sophia", "Lee", "Lee Digital", "+82-10-1234-5678", "Gangnam-gu", "Seoul", null, "06000", "KR", ClientStatus.Active),
            ("noah.martin@example.com", "Noah", "Martin", null, "+33-6-12-34-56-78", "8 Rue de Rivoli", "Paris", null, "75001", "FR", ClientStatus.Inactive),
            ("ava.taylor@example.com", "Ava", "Taylor", "Taylor Media", "+61-2-1234-5678", "42 George St", "Sydney", "NSW", "2000", "AU", ClientStatus.Active),
            ("ethan.davis@example.com", "Ethan", "Davis", null, null, null, null, null, null, null, ClientStatus.Active),
            ("mia.anderson@example.com", "Mia", "Anderson", "Anderson Corp", "+1-555-0112", "555 Broadway", "New York", "NY", "10012", "US", ClientStatus.Active),
            ("lucas.thomas@example.com", "Lucas", "Thomas", null, "+49-30-12345678", "Friedrichstr. 100", "Berlin", null, "10117", "DE", ClientStatus.Active),
            ("isabella.white@example.com", "Isabella", "White", "White Cloud Hosting", "+1-555-0114", "900 Cloud Dr", "Seattle", "WA", "98101", "US", ClientStatus.Closed),
            ("james.harris@example.com", "James", "Harris", null, "+1-555-0115", "77 Sunset Blvd", "Los Angeles", "CA", "90028", "US", ClientStatus.Active),
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

                // Spread signups across the last 6 months for the New Customers report
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
        // Gated on Quotes being empty: this is the one-time "top-up" sentinel, so the
        // backdated invoices are added once even when single-date invoices already exist.
        if (!await db.Quotes.AnyAsync(ct))
        {
            var invoices = new List<Invoice>();

            // Products and amounts for realistic data
            string[] products = ["Shared Hosting Basic", "Shared Hosting Pro", "VPS Server", "Dedicated Server", "SSL Certificate", "Domain Registration", "Email Hosting", "CDN Service", "Backup Service", "Firewall Service"];
            decimal[] prices = [9.99m, 29.99m, 49.99m, 99.99m, 14.99m, 12.99m, 19.99m, 24.99m, 7.99m, 39.99m];

            foreach (var client in clients)
            {
                // One invoice per month for last 6 months
                for (int monthsBack = 5; monthsBack >= 0; monthsBack--)
                {
                    var invoiceDate = MonthsAgo(monthsBack, Rng.Next(1, 20));
                    var dueDate = invoiceDate.AddDays(30);
                    var productIndex = Rng.Next(products.Length);

                    var invoice = Invoice.Create(client.Id, dueDate);
                    invoice.AddItem(products[productIndex], prices[productIndex], 1);

                    // Add extra item sometimes
                    if (Rng.Next(3) == 0)
                    {
                        var idx2 = Rng.Next(products.Length);
                        invoice.AddItem(products[idx2], prices[idx2], 1);
                    }

                    // Assign status based on age
                    if (monthsBack >= 4)
                    {
                        invoice.MarkPaid("stripe_" + Rng.Next(100000, 999999));
                    }
                    else if (monthsBack == 3)
                    {
                        if (Rng.Next(2) == 0)
                            invoice.MarkPaid("stripe_" + Rng.Next(100000, 999999));
                        else
                            invoice.MarkOverdue();
                    }
                    else if (monthsBack == 2)
                    {
                        if (Rng.Next(3) == 0)
                            invoice.MarkOverdue();
                    }
                    // Recent months: unpaid

                    db.Invoices.Add(invoice);
                    invoices.Add(invoice);

                    // Backdate CreatedAt through the change tracker (no raw SQL)
                    db.Entry(invoice).Property(nameof(Invoice.CreatedAt)).CurrentValue = invoiceDate;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} invoices across 6 months", invoices.Count);
        }

        // ── Invoice Transactions (payments) ──────────────────────────────────
        if (!await db.InvoiceTransactions.AnyAsync(ct))
        {
            var paidInvoices = await db.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid)
                .ToListAsync(ct);

            foreach (var inv in paidInvoices)
            {
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
                // Credit payment (client paid money)
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
                    var product = Product.Create(group.Id, name, null, null, null, productType, monthly, annual);
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

        // ── Product-matched invoices (for Income by Product report) ──────────
        // Seed paid invoices whose item descriptions match real product names,
        // so GetIncomeByProductGroupedAsync can join them correctly.
        if (!hasProductInvoices)
        {
            var products = await db.Products.ToListAsync(ct);
            var activeClients = await db.Clients.Where(c => c.Status == ClientStatus.Active).ToListAsync(ct);

            // Seed paid invoices for the last 3 months
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

        // ── SSL check cache (seed only, no real domains created) ─────────────
        if (!await db.SslChecks.AnyAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            (string domain, bool hasSsl, string? issuer, DateTimeOffset? expires, bool isActive)[] sslDefs =
            [
                ("google.com",      true,  "Google Trust Services", now.AddDays(60),  true),
                ("github.com",      true,  "DigiCert Inc",          now.AddDays(45),  true),
                ("cloudflare.com",  true,  "Google Trust Services", now.AddDays(75),  true),
                ("microsoft.com",   true,  "DigiCert Inc",          now.AddDays(200), true),
                ("amazon.com",      true,  "Amazon",                now.AddDays(250), true),
                ("letsencrypt.org", true,  "Let's Encrypt",         now.AddDays(20),  true),
                ("expired.local",   false, null,                    null,             false),
                ("no-ssl.local",    false, null,                    null,             true),
            ];

            foreach (var (domain, hasSsl, issuer, expires, isActive) in sslDefs)
                db.SslChecks.Add(SslCheck.Create(domain, hasSsl, issuer, expires, isActive));

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded SSL checks");
        }

        logger.LogInformation("Dev seed complete");
    }
}
