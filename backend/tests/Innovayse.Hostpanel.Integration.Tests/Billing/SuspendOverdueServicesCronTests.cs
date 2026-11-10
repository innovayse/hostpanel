using Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="SuspendOverdueServicesCronHandler"/> against Postgres: only the Active services on lines of
/// invoices overdue past the grace period (default 3 days) are suspended, with source Overdue; services
/// outside those invoices, inside the grace, or protected by an override are left alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SuspendOverdueServicesCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host with a suppressed event gate, optionally poisoning one service's save or racing a payment.</summary>
    /// <param name="poisoned">Names the service whose suspension save throws, or null for none.</param>
    /// <param name="race">Names the suspension whose save first pays another invoice, or null for none.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> Host(PoisonedService? poisoned = null, PaymentRace? race = null) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddScoped<IDomainEventGate, SuppressedDomainEventGate>();
            if (poisoned is not null)
            {
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new PoisonedServiceSaveInterceptor(poisoned)));
            }

            if (race is not null)
            {
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new PayingSaveInterceptor(race)));
            }
        }));

    /// <summary>Seeds a client with a product to attach services to.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The client id and product id.</returns>
    private static async Task<ClientProductIds> SeedClientAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Grace", "Hopper", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        var group = ProductGroup.Create($"overdue-cron-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        return new(client.Id, product.Id);
    }

    /// <summary>Seeds an active service for a client, optionally protected from auto-suspend.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="productId">The product.</param>
    /// <param name="overrideAutoSuspend">Whether the service opts out of auto-suspend.</param>
    /// <param name="suspendUntil">A date until which auto-suspend is held off, or null.</param>
    /// <param name="autoRenew">Whether billing looks after the service; false means staff bill it by hand.</param>
    /// <returns>The service id.</returns>
    private static async Task<int> SeedServiceAsync(
        IHostpanelDbContext context, int clientId, int productId, bool overrideAutoSuspend = false, DateTimeOffset? suspendUntil = null,
        bool autoRenew = true)
    {
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
        if (overrideAutoSuspend || suspendUntil is not null || !autoRenew)
        {
            service.ChangeBillingTerms(new ServiceBillingTerms(
                BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m,
                RecurringAmount: 5m, NextRenewalAt: service.NextRenewalAt, PaymentMethod: null,
                PromotionCode: null, SubscriptionId: null));
            service.ConfigureAutomation(autoRenew, overrideAutoSuspend, suspendUntil, false, null);
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }

    /// <summary>Seeds an invoice with one line per service, due the given days ago, and marks it overdue.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="serviceIds">Services to put on lines; empty yields a plain, service-less line.</param>
    /// <param name="dueDaysAgo">How many days ago the invoice fell due.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> SeedOverdueInvoiceAsync(
        IHostpanelDbContext context, int clientId, IReadOnlyList<int> serviceIds, int dueDaysAgo)
    {
        var now = DateTimeOffset.UtcNow;
        var due = now.AddDays(-dueDaysAgo);
        var invoice = Invoice.Create(clientId, due, "USD", now);
        if (serviceIds.Count == 0)
        {
            invoice.AddItem("One-off setup", 25m, 1);
        }

        foreach (var id in serviceIds)
        {
            invoice.AddItem("Hosting", 25m, 1, id);
        }

        invoice.UpdateOptions(due.AddDays(-14), due, null, 0m);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        invoice.MarkOverdue(now);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Runs the cron once.</summary>
    /// <param name="services">The scoped service provider.</param>
    /// <returns>A task.</returns>
    private static async Task RunAsync(IServiceProvider services) =>
        await services.GetRequiredService<IMessageBus>().InvokeAsync(new SuspendOverdueServicesCronCommand(), CancellationToken.None);

    /// <summary>Reloads a service without tracking.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="id">The service id.</param>
    /// <returns>The service as stored.</returns>
    private static Task<ClientService> LoadAsync(IHostpanelDbContext context, int id) =>
        context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == id, CancellationToken.None);

    /// <summary>An invoice overdue past the grace suspends exactly its services, with source Overdue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PastGrace_SuspendsOnlyItsServices()
    {
        using var scope = Host().Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var onInvoice = await SeedServiceAsync(context, clientId, productId);
        var notOnInvoice = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [onInvoice], dueDaysAgo: 4);

        await RunAsync(scope.ServiceProvider);

        var suspended = await LoadAsync(context, onInvoice);
        suspended.Status.Should().Be(ServiceStatus.Suspended);
        suspended.SuspensionSource.Should().Be(SuspensionSource.Overdue);
        (await LoadAsync(context, notOnInvoice)).Status.Should().Be(ServiceStatus.Active);

        // A second run finds the service no longer Active and does nothing.
        var second = async () => await RunAsync(scope.ServiceProvider);
        await second.Should().NotThrowAsync();
        (await LoadAsync(context, onInvoice)).Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>An invoice overdue for less than the grace period suspends nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InsideGrace_Nothing()
    {
        using var scope = Host().Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var service = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [service], dueDaysAgo: 1);

        await RunAsync(scope.ServiceProvider);

        (await LoadAsync(context, service)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// A suspension hold (OverrideAutoSuspend) protects a service, for good or until a future
    /// SuspendUntil; a hold whose SuspendUntil has passed does not.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Overrides_Protect()
    {
        using var scope = Host().Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var overridden = await SeedServiceAsync(context, clientId, productId, overrideAutoSuspend: true);
        var held = await SeedServiceAsync(
            context, clientId, productId, overrideAutoSuspend: true, suspendUntil: DateTimeOffset.UtcNow.AddDays(5));
        var elapsed = await SeedServiceAsync(
            context, clientId, productId, overrideAutoSuspend: true, suspendUntil: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedOverdueInvoiceAsync(context, clientId, [overridden, held, elapsed], dueDaysAgo: 4);

        await RunAsync(scope.ServiceProvider);

        (await LoadAsync(context, overridden)).Status.Should().Be(ServiceStatus.Active);
        (await LoadAsync(context, held)).Status.Should().Be(ServiceStatus.Active);
        (await LoadAsync(context, elapsed)).Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>A service billed by hand (<c>AutoRenew</c> off) is never suspended by the overdue run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualBilling_IsNotSuspended()
    {
        using var scope = Host().Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var manual = await SeedServiceAsync(context, clientId, productId, autoRenew: false);
        var automatic = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [manual, automatic], dueDaysAgo: 4);

        await RunAsync(scope.ServiceProvider);

        (await LoadAsync(context, manual)).Status.Should().Be(ServiceStatus.Active);
        (await LoadAsync(context, automatic)).Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>An overdue invoice with no service lines suspends nothing: only the services an invoice bills are suspended, never the client's other services.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoServiceLines_Nothing()
    {
        using var scope = Host().Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var service = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [], dueDaysAgo: 10);

        await RunAsync(scope.ServiceProvider);

        (await LoadAsync(context, service)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>One suspension whose save throws does not stop another service from being suspended.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OneFailingSuspension_DoesNotStopTheOthers()
    {
        var poisoned = new PoisonedService();
        using var scope = Host(poisoned).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);
        var bad = await SeedServiceAsync(context, clientId, productId);
        var good = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [bad, good], dueDaysAgo: 4);
        poisoned.ServiceId = bad;

        await RunAsync(scope.ServiceProvider);

        (await LoadAsync(context, good)).Status.Should().Be(ServiceStatus.Suspended);
        (await LoadAsync(context, bad)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// A payment that lands while the run is between its query and a later suspension is honoured:
    /// the second service's invoice is paid while the first service is being suspended (after the
    /// run listed both), and the second service is not suspended.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentDuringTheRun_IsNotAnsweredWithASuspension()
    {
        var race = new PaymentRace();
        using var scope = Host(race: race).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAsync(context);

        // Ids ascend, and the run suspends in id order: first is handled before second.
        var first = await SeedServiceAsync(context, clientId, productId);
        var second = await SeedServiceAsync(context, clientId, productId);
        await SeedOverdueInvoiceAsync(context, clientId, [first], dueDaysAgo: 4);
        var secondInvoice = await SeedOverdueInvoiceAsync(context, clientId, [second], dueDaysAgo: 4);
        race.TriggerServiceId = first;
        race.InvoiceToPay = secondInvoice;

        await RunAsync(scope.ServiceProvider);

        race.Fired.Should().BeTrue("the payment must land mid-run for this test to mean anything");
        (await LoadAsync(context, first)).Status.Should().Be(ServiceStatus.Suspended);
        (await LoadAsync(context, second)).Status.Should().Be(ServiceStatus.Active);
    }
}
