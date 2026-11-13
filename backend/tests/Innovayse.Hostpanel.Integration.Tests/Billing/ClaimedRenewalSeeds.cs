using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>Seeds for the tests of domain renewal lines left claimed after an unknown registrar outcome.</summary>
public static class ClaimedRenewalSeeds
{
    /// <summary>Seeds a paid domain renewal invoice whose line is claimed and unapplied, as a timed-out registrar call leaves it.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="claim">Whether the line is claimed; <see langword="false"/> leaves it untouched.</param>
    /// <param name="claimedAt">When the claim was taken, or <see langword="null"/> for two days ago.</param>
    /// <returns>The ids.</returns>
    public static async Task<ClaimedRenewalLine> SeedAsync(IntegrationTestFactory factory, bool claim = true, DateTimeOffset? claimedAt = null)
    {
        // Seeded on a host whose domain-event gate is suppressed: the paid-invoice event would otherwise start the renewal
        // sweep, which claims, applies or releases the line on its own while the test is setting its state up.
        using var scope = EventHandlerTestHost.WithFakeRelay(factory, new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var expiry = DateTimeOffset.UtcNow.AddDays(5);
        var domain = RegisteredDomain.Register(
            client.Id, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, expiry, autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, expiry, "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        if (claim)
        {
            var at = claimedAt ?? DateTimeOffset.UtcNow.AddDays(-2);
            await context.InvoiceItems.Where(l => l.InvoiceId == invoice.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAttemptAt, (DateTimeOffset?)at), CancellationToken.None);
        }

        var itemId = await context.InvoiceItems.Where(l => l.InvoiceId == invoice.Id).Select(l => l.Id).SingleAsync();
        return new(client.Id, invoice.Id, itemId);
    }
}
