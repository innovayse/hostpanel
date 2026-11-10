using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and reading helpers shared by the plan change tests (<c>Services/*ServiceChange*Tests.cs</c> and the plan change
/// option and request tests): a service on one plan with a second plan beside it, and the requests made against it.
/// </summary>
public static class ServiceChangeTestSeeds
{
    /// <summary>The days left in the current cycle that a freshly seeded service has, so a test can work out the exact amount.</summary>
    public const int DaysLeft = 15;

    /// <summary>
    /// Seeds an active monthly service on a product priced <paramref name="fromPrice"/> in USD, with a second product of the
    /// same group priced <paramref name="toPrice"/>, and a renewal date <see cref="DaysLeft"/> days away.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The owning client, or <see langword="null"/> to seed a new one.</param>
    /// <param name="fromPrice">The monthly price of the product the service runs on; also the service's recurring amount.</param>
    /// <param name="toPrice">The monthly price of the other product.</param>
    /// <param name="provisioned">
    /// Whether the service holds a hosting account on a seeded server, so the panel is asked for the new package; otherwise it has none.
    /// </param>
    /// <param name="toPackage">The hosting package the other product names, or <see langword="null"/> for none.</param>
    /// <param name="renewalIn">When the current cycle ends, from now; <see cref="DaysLeft"/> days when <see langword="null"/>.</param>
    /// <returns>The seeded ids and prices.</returns>
    public static async Task<SeededPlanChange> SeedServiceAsync(
        IntegrationTestFactory factory,
        int? clientId,
        decimal fromPrice,
        decimal toPrice,
        bool provisioned = false,
        string? toPackage = "pro",
        TimeSpan? renewalIn = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;

        var ownerId = clientId ?? await SeedClientAsync(context);
        var group = ProductGroup.Create($"plan-change-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var from = Product.Create(group.Id, "Starter", null, null, null, "starter", ProductType.SharedHosting, 0m, 0m, now);
        from.SetPrice("USD", BillingCycle.Monthly, fromPrice);
        var to = Product.Create(group.Id, "Pro", null, null, null, toPackage, ProductType.SharedHosting, 0m, 0m, now);
        to.SetPrice("USD", BillingCycle.Monthly, toPrice);
        context.Products.Add(from);
        context.Products.Add(to);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.CreateOrdered(
            ownerId, from.Id, BillingCycle.Monthly, null, fromPrice, fromPrice, null, null, now);
        if (provisioned)
        {
            var server = await ProvisioningTestSeeds.SeedServerAsync(context);
            await ServicesTestSeeds.AddActivatedAsync(context, service, $"acct-{Guid.NewGuid():N}", now, server.Id);
        }
        else
        {
            context.ClientServices.Add(service);
            await context.SaveChangesAsync(CancellationToken.None);
            service.ActivateDigital(now);
        }

        var renewal = now + (renewalIn ?? TimeSpan.FromDays(DaysLeft));
        service.ChangeBillingTerms(new ServiceBillingTerms(BillingCycle.Monthly, 1, fromPrice, fromPrice, renewal, null, null, null));
        await context.SaveChangesAsync(CancellationToken.None);

        return new SeededPlanChange(ownerId, service.Id, from.Id, to.Id, fromPrice, toPrice, renewal);
    }

    /// <summary>
    /// The amount a move between two prices costs with <see cref="DaysLeft"/> days left of a monthly cycle ending at
    /// <paramref name="renewal"/>, worked out here from the rule itself so a test does not ask the calculator for its own answer.
    /// </summary>
    /// <param name="fromPrice">The current monthly price.</param>
    /// <param name="toPrice">The new monthly price.</param>
    /// <param name="renewal">The end of the cycle.</param>
    /// <returns>The prorated amount in USD, rounded to cents.</returns>
    public static decimal ExpectedProration(decimal fromPrice, decimal toPrice, DateTimeOffset renewal)
    {
        var cycleDays = (int)(renewal - renewal.AddMonths(-1)).TotalDays;
        return Math.Round(Math.Abs(toPrice - fromPrice) * DaysLeft / cycleDays, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Adds a plan change request for a seeded service, in the given status, through the entity's own methods.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="seeded">The seeded service.</param>
    /// <param name="status">The status to leave the request in: Pending, Approved, Completed or Rejected.</param>
    /// <param name="invoiceId">The proration invoice an approved upgrade waits for, or <see langword="null"/> for none.</param>
    /// <returns>The new request's id.</returns>
    public static async Task<int> SeedRequestAsync(
        IntegrationTestFactory factory, SeededPlanChange seeded, ServiceChangeStatus status, int? invoiceId = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;

        var type = seeded.ToPrice > seeded.FromPrice ? ServiceChangeType.Upgrade : ServiceChangeType.Downgrade;
        var request = ServiceChangeRequest.Create(
            seeded.ServiceId,
            seeded.ClientId,
            seeded.FromProductId,
            seeded.ToProductId,
            new ProratedPlanChange(type, ExpectedProration(seeded.FromPrice, seeded.ToPrice, seeded.NextRenewalAt)),
            "USD",
            seeded.ToPrice,
            now);
        if (status is ServiceChangeStatus.Rejected)
        {
            request.Reject("Not now", now);
        }
        else if (status is not ServiceChangeStatus.Pending)
        {
            request.Approve();
            if (invoiceId is { } invoice)
            {
                request.AttachInvoice(invoice);
            }

            if (status is ServiceChangeStatus.Completed)
            {
                request.Complete(now);
            }
        }

        context.ServiceChangeRequests.Add(request);
        await context.SaveChangesAsync(CancellationToken.None);
        return request.Id;
    }

    /// <summary>Seeds an unpaid invoice of the given amount for a client, the proration invoice an upgrade waits for.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="amount">The amount of the one line.</param>
    /// <returns>The new invoice's id.</returns>
    public static async Task<int> SeedInvoiceAsync(IntegrationTestFactory factory, int clientId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Upgrade to Pro", amount, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Records the full payment of an invoice through the admin route, the way a manual payment is booked.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="invoiceId">The invoice to pay.</param>
    /// <param name="amount">The amount paid.</param>
    /// <returns>A task that completes when the payment is recorded.</returns>
    public static async Task PayInvoiceAsync(HttpClient admin, int invoiceId, decimal amount)
    {
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount,
            fees = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Reads a plan change request as the database holds it.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="requestId">The request.</param>
    /// <returns>The request, untracked.</returns>
    public static async Task<ServiceChangeRequest> ReadRequestAsync(IntegrationTestFactory factory, int requestId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ServiceChangeRequests.AsNoTracking().FirstAsync(r => r.Id == requestId);
    }

    /// <summary>Reads a service as the database holds it.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The service, untracked.</returns>
    public static async Task<ClientService> ReadServiceAsync(IntegrationTestFactory factory, int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId);
    }

    /// <summary>Reads a client's credit balance.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The balance.</returns>
    public static async Task<decimal> CreditOfAsync(IntegrationTestFactory factory, int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return (await context.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId)).CreditBalance;
    }

    /// <summary>Counts the plan change credit lines on a client's ledger.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>How many ledger entries carry the plan change credit movement.</returns>
    public static async Task<int> PlanChangeCreditLinesOfAsync(IntegrationTestFactory factory, int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking()
            .CountAsync(t => t.ClientId == clientId && t.CreditMovement == CreditMovementType.PlanChangeCredit);
    }

    /// <summary>Switches the automatic approval of downgrades on or off.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="on">The value to store.</param>
    /// <returns>A task that completes when the setting is stored.</returns>
    public static async Task SetAutoApproveDowngradesAsync(IntegrationTestFactory factory, bool on)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.AutoApproveDowngrades);
        row.UpdateValue(on ? "true" : "false");
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Seeds a client row with no credit.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The new client's id.</returns>
    private static async Task<int> SeedClientAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }
}
