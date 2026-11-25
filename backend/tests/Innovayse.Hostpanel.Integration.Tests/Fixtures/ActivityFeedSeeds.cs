using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding helpers shared by the notification feed tests (the feed itself and the two read marks): a client,
/// an invoice and a ticket on a clocked host, so every test states only the moments that make it different.
/// </summary>
internal static class ActivityFeedSeeds
{
    /// <summary>Creates a client named Ada with the given last name, created at a moment.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="lastName">The last name, unique to the test so its events can be told apart.</param>
    /// <param name="createdAt">When the client registered.</param>
    /// <returns>The new client id.</returns>
    public static async Task<int> SeedClientAsync(ClockedHost host, string lastName, DateTimeOffset createdAt)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", lastName, $"ada-{Guid.NewGuid():N}@example.test", createdAt);
        client.Backdate(createdAt);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Creates an invoice for a client, raised at a moment, and lets the caller move it on.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="amount">The invoice total.</param>
    /// <param name="raisedAt">When the invoice was raised.</param>
    /// <param name="advance">What happens to the invoice before it is saved.</param>
    /// <returns>The new invoice id.</returns>
    public static async Task<int> SeedInvoiceAsync(ClockedHost host, int clientId, decimal amount, DateTimeOffset raisedAt, Action<Invoice> advance)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var code = (await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None)).Code;
        var invoice = Invoice.Create(clientId, raisedAt.AddDays(14), code, raisedAt);
        invoice.Backdate(raisedAt);
        invoice.AddItem("Hosting", amount, 1);
        advance(invoice);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Creates a support ticket for a client, opened at a moment, and lets the caller move it on.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="clientId">The client who opened it.</param>
    /// <param name="subject">The ticket subject.</param>
    /// <param name="openedAt">When the ticket was opened.</param>
    /// <param name="advance">What happens to the ticket before it is saved; nothing when omitted.</param>
    /// <returns>The new ticket id.</returns>
    public static async Task<int> SeedTicketAsync(ClockedHost host, int clientId, string subject, DateTimeOffset openedAt, Action<Ticket>? advance = null)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var ticket = Ticket.Create(clientId, subject, "Please help", departmentId, TicketPriority.Medium, openedAt);
        advance?.Invoke(ticket);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);
        return ticket.Id;
    }
}
