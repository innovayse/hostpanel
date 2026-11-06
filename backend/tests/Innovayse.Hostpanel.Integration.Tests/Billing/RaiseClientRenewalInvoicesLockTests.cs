using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The per-client lock on <see cref="RaiseClientRenewalInvoicesCommand"/> against Postgres: two callers for one
/// client (the daily sweep and a staff click, or two instances) never raise the same renewal twice, and the staff
/// endpoint <c>POST /api/billing/client/{id}/renewal-invoices</c> runs the same use case.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RaiseClientRenewalInvoicesLockTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose domain-event gate is suppressed, so only what the command itself does is observed.</summary>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> Host() => EventHandlerTestHost.WithFakeRelay(factory, new FakeEmailSender());

    /// <summary>Seeds a client with one active, auto-renewing service due inside the lead time.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> SeedDueClientAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        var group = ProductGroup.Create($"lock-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
        service.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m, RecurringAmount: 5m,
            NextRenewalAt: DateTimeOffset.UtcNow.AddDays(5), PaymentMethod: null, PromotionCode: null, SubscriptionId: null));
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Counts the client's renewal invoices.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>How many invoices exist.</returns>
    private static async Task<int> InvoiceCountAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId);
    }

    /// <summary>Several callers racing for one client raise exactly one invoice between them.</summary>
    [Fact]
    public async Task ConcurrentCallers_RaiseOneInvoice()
    {
        var host = Host();
        int clientId;
        using (var seed = host.Services.CreateScope())
        {
            clientId = await SeedDueClientAsync(seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        var calls = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            using var scope = host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), CancellationToken.None);
        })).ToArray();
        var results = await Task.WhenAll(calls);

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Sum(r => r.Value).Should().Be(1, "only the caller that held the lock first had anything to raise");
        (await InvoiceCountAsync(host, clientId)).Should().Be(1);
    }

    /// <summary>
    /// A caller waits while another transaction holds the client lock, and raises only after it is released:
    /// this is the lock itself, not luck in the timing of two callers.
    /// </summary>
    [Fact]
    public async Task ACaller_WaitsWhileTheClientLockIsHeld()
    {
        var host = Host();
        using var holderScope = host.Services.CreateScope();
        var holder = holderScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await SeedDueClientAsync(holder);

        Task<Result<int>> waiting;
        await using (var transaction = await holder.BeginTransactionAsync(CancellationToken.None))
        {
            await holder.LockForTransactionAsync(RaiseClientRenewalInvoicesHandler.LockKeyOf(clientId), CancellationToken.None);

            waiting = Task.Run(async () =>
            {
                using var scope = host.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                    .InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), CancellationToken.None);
            });

            var finishedEarly = await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromSeconds(2))) == waiting;
            finishedEarly.Should().BeFalse("the lock is held, so the caller must wait for it");
            (await InvoiceCountAsync(host, clientId)).Should().Be(0);

            await transaction.CommitAsync(CancellationToken.None);
        }

        var result = await waiting.WaitAsync(TimeSpan.FromSeconds(30));
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        (await InvoiceCountAsync(host, clientId)).Should().Be(1);
    }

    /// <summary>
    /// The balance is read after the lock is taken: credit spent by the run that held the lock first is not applied a second time.
    /// While the caller waits for the lock the client's balance is spent elsewhere; the invoice it raises then carries no credit.
    /// </summary>
    [Fact]
    public async Task TheBalance_IsReadAfterTheLock_NotBeforeTheWait()
    {
        var host = Host();
        using var holderScope = host.Services.CreateScope();
        var holder = holderScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await SeedDueClientAsync(holder);
        await BillingTestSeeds.GiveCreditAsync(host, clientId, 5m);

        Task<Result<int>> waiting;
        await using (var transaction = await holder.BeginTransactionAsync(CancellationToken.None))
        {
            await holder.LockForTransactionAsync(RaiseClientRenewalInvoicesHandler.LockKeyOf(clientId), CancellationToken.None);

            waiting = Task.Run(async () =>
            {
                using var scope = host.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                    .InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), CancellationToken.None);
            });
            (await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromSeconds(2)))).Should().NotBe(waiting);

            // The run that held the lock spent the credit on its own invoice.
            using (var spender = host.Services.CreateScope())
            {
                var other = spender.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
                var client = await other.Clients.SingleAsync(c => c.Id == clientId, CancellationToken.None);
                client.DeductCredit(5m);
                await other.SaveChangesAsync(CancellationToken.None);
            }

            await transaction.CommitAsync(CancellationToken.None);
        }

        var result = await waiting.WaitAsync(TimeSpan.FromSeconds(30));
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.ClientId == clientId);
        invoice.Credit.Should().Be(0m, "the credit was already spent when the lock was released");
        (await context.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId)).CreditBalance.Should().Be(0m);
    }

    /// <summary>The staff endpoint raises the due invoice once, answers the count, logs the run, and a second click raises nothing.</summary>
    [Fact]
    public async Task StaffEndpoint_RaisesOnce_AndLogsTheRun()
    {
        int clientId;
        using (var seed = factory.Services.CreateScope())
        {
            clientId = await SeedDueClientAsync(seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var first = await admin.PostAsync($"/api/billing/client/{clientId}/renewal-invoices", null);
        var second = await admin.PostAsync($"/api/billing/client/{clientId}/renewal-invoices", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<int>()).Should().Be(1);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<int>()).Should().Be(0);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId)).Should().Be(1);
        var logs = await context.ActivityLogs.AsNoTracking()
            .Where(l => l.ClientId == clientId && l.Description.Contains("Generated Renewal Invoices"))
            .Select(l => l.Description)
            .ToListAsync();
        logs.Should().ContainSingle().Which.Should().EndWith("Invoices: 1");
    }

    /// <summary>An unknown client answers 404 <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task StaffEndpoint_OnAnUnknownClient_Returns404()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/client/{BillingTestSeeds.NoSuchId}/renewal-invoices", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Anonymous callers get 401 and a client who is not staff gets 403.</summary>
    [Fact]
    public async Task StaffEndpoint_RefusesNonStaff()
    {
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        (await factory.CreateClient().PostAsync("/api/billing/client/1/renewal-invoices", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stranger.PostAsync("/api/billing/client/1/renewal-invoices", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
