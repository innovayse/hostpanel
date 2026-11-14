using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builder for the invoice list tests: invoices seeded straight into the database at a chosen moment, with a chosen
/// payment method and status.
/// </summary>
public static class InvoiceListTestSeeds
{
    /// <summary>Seeds one invoice of one line for a client, created at the given moment.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="createdAt">The moment the invoice was created (UTC); now when not given.</param>
    /// <param name="paymentMethod">The payment method stored on the invoice, or null for none.</param>
    /// <param name="isDraft">Whether the invoice is a draft rather than unpaid.</param>
    /// <returns>The new invoice id.</returns>
    public static async Task<int> SeedAsync(
        WebApplicationFactory<Program> host,
        int clientId,
        DateTimeOffset? createdAt = null,
        string? paymentMethod = null,
        bool isDraft = false)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var created = createdAt ?? DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(clientId, created.AddDays(14), "USD", isDraft, created);
        invoice.AddItem("Hosting", 10m, 1, null);
        invoice.UpdateOptions(created, created.AddDays(14), paymentMethod, 0m);
        invoice.Backdate(created);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }
}
