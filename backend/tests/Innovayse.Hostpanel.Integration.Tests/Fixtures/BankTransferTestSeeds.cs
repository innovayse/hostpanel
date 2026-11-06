using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared set-up for the manual bank transfer tests: the integration switched on with bank details, a host whose mail relay
/// is a fake but whose domain events still flow, and the steps that bring an invoice to the state a test starts from.
/// </summary>
public static class BankTransferTestSeeds
{
    /// <summary>The staff address the mail host is configured with.</summary>
    public const string StaffEmail = "ops-bank@example.test";

    /// <summary>The IBAN the tests save as the operator's bank details.</summary>
    public const string Iban = "AM00 0000 0000 0000";

    /// <summary>How long a tracked action may take before the test gives up, generous for a first-use handler compile.</summary>
    private static readonly TimeSpan TrackedTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Builds a host whose SMTP relay is <paramref name="sender"/> and whose staff address is <see cref="StaffEmail"/>.
    /// Unlike the event-handler host it keeps the domain-event gate, so an HTTP action publishes its events as in production.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="sender">The fake relay the mails are written to.</param>
    /// <returns>The isolated host.</returns>
    public static WebApplicationFactory<Program> HostWithMail(IntegrationTestFactory factory, FakeEmailSender sender) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{NotificationOptions.SectionName}:AdminEmail"] = StaffEmail }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });

    /// <summary>Switches the bank transfer integration on through the real admin endpoint, with bank details.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <returns>A task that completes once the settings are saved.</returns>
    public static async Task EnableAsync(HttpClient admin)
    {
        var response = await admin.PutAsJsonAsync(
            "/api/admin/integrations/bank-transfer",
            new
            {
                isEnabled = true,
                config = new Dictionary<string, string>
                {
                    ["account_name"] = "Example Hosting LLC",
                    ["iban"] = Iban,
                    ["swift"] = "EXAMPAMX",
                    ["bank_name"] = "Example Bank",
                    ["instructions"] = "Pay within five days.",
                },
            });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Switches the bank transfer integration off through the real admin endpoint.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <returns>A task that completes once the switch is off.</returns>
    public static async Task DisableAsync(HttpClient admin)
    {
        var response = await admin.PutAsJsonAsync(
            "/api/admin/integrations/bank-transfer",
            new { isEnabled = false, config = new Dictionary<string, string>() });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Runs an action and waits until every message it set off has been handled, so a test can read the mail it caused.</summary>
    /// <param name="host">The host whose bus is watched.</param>
    /// <param name="action">The action, usually one HTTP call.</param>
    /// <returns>A task that completes once the host is quiet again.</returns>
    public static async Task RunTrackedAsync(WebApplicationFactory<Program> host, Func<Task> action)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TrackedTimeout)
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => action()));
    }

    /// <summary>Creates an invoice for a client as the admin and lets the client choose the bank transfer for it.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="owner">The invoiced client's own session.</param>
    /// <param name="clientId">The invoiced client's id.</param>
    /// <param name="amount">The invoice amount.</param>
    /// <returns>The id of the invoice now waiting for the transfer.</returns>
    public static async Task<int> PendingInvoiceAsync(HttpClient admin, HttpClient owner, int clientId, decimal amount = 100m)
    {
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, amount);
        var selected = await owner.PostAsync($"/api/me/invoices/{invoiceId}/bank-transfer", content: null);
        selected.StatusCode.Should().Be(HttpStatusCode.OK);
        return invoiceId;
    }

    /// <summary>Raises a Pending order and links it to an invoice, as an order checkout does.</summary>
    /// <param name="host">The host whose database holds the invoice.</param>
    /// <param name="invoiceId">The invoice that pays the order.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <returns>The order's id.</returns>
    public static async Task<int> LinkOrderAsync(WebApplicationFactory<Program> host, int invoiceId, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var order = Order.Create(
            $"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank_transfer", ipAddress: null, currency: "USD", now: DateTimeOffset.UtcNow);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);
        order.LinkInvoice(invoiceId);
        await context.SaveChangesAsync(CancellationToken.None);
        return order.Id;
    }
}
