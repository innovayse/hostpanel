using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ReconcileGatewayPaymentsCronHandler"/> against Postgres: which unpaid/overdue
/// invoices it picks up once their gateway session is old enough to have left the payment page,
/// and how a settled invoice whose kept session later reports paid is credited once.
/// </summary>
/// <remarks>
/// <para>
/// A picked-up invoice is observed indirectly: the reconciler dispatches
/// <c>CompleteGatewayPaymentCommand</c> for each match, which calls
/// <see cref="IPaymentPlugin.GetStatusAsync"/> exactly once per invoice it actually reconciles
/// (an invoice already Paid or with no session short-circuits before that call, but every
/// invoice this suite seeds is Unpaid/Overdue with a session, so
/// <see cref="FakePaymentPlugin.GetStatusCallCount"/> counts precisely which invoices the
/// window's filter selected).
/// </para>
/// <para>
/// <see cref="IntegrationTestFactory"/> is a class fixture, so the same Postgres database is
/// shared by every test method here; the reconciler's query is not scoped to one client, so a
/// module id shared across tests would let one test's leftover invoice be resolved, and
/// counted, against a later test's plugin. Each test therefore mints its own module id, so an
/// older test's invoice (still in the window when a later test runs moments later) resolves to
/// no plugin at all for that later test and is silently skipped, exactly as an unconfigured
/// module id is in production.
/// </para>
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReconcileGatewayPaymentsCronTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>
    /// Seeds a client and one invoice carrying a gateway session under <paramref name="module"/>,
    /// started at <paramref name="gatewayStartedAt"/>.
    /// </summary>
    /// <param name="db">The data context to seed through.</param>
    /// <param name="module">This test's own plugin module id.</param>
    /// <param name="status">The invoice's status; <see cref="InvoiceStatus.Unpaid"/> or <see cref="InvoiceStatus.Overdue"/>.</param>
    /// <param name="gatewayStartedAt">The backdated session start instant.</param>
    /// <returns>The seeded invoice's primary key.</returns>
    private static async Task<int> SeedPendingInvoiceAsync(
        AppDbContext db, string module, InvoiceStatus status, DateTimeOffset gatewayStartedAt)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Gateway", "Payer", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        if (status == InvoiceStatus.Overdue)
        {
            invoice.MarkOverdue(DateTimeOffset.UtcNow);
        }

        invoice.SetGatewaySession(module, $"gw-{Guid.NewGuid():N}", gatewayStartedAt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        return invoice.Id;
    }

    /// <summary>
    /// An Unpaid invoice whose gateway session started 1 hour ago (inside the 25-minute-to-24-hour
    /// window) is reconciled.
    /// </summary>
    [Fact]
    public async Task UnpaidInvoiceWithinWindow_IsReconciled()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var host = BuildHost(resolver);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPendingInvoiceAsync(db, module, InvoiceStatus.Unpaid, DateTimeOffset.UtcNow.AddHours(-1));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>
    /// An Overdue invoice within the window is reconciled too: the filter ORs Unpaid and
    /// Overdue, it does not restrict to Unpaid alone.
    /// </summary>
    [Fact]
    public async Task OverdueInvoiceWithinWindow_IsAlsoReconciled()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var host = BuildHost(resolver);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPendingInvoiceAsync(db, module, InvoiceStatus.Overdue, DateTimeOffset.UtcNow.AddHours(-1));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>
    /// A session that started 10 minutes ago is younger than the 25-minute settle margin:
    /// the payer may still be on the payment page, so it is left alone.
    /// </summary>
    [Fact]
    public async Task SessionYoungerThanSettleMargin_IsNotReconciled()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var host = BuildHost(resolver);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPendingInvoiceAsync(db, module, InvoiceStatus.Unpaid, DateTimeOffset.UtcNow.AddMinutes(-10));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        plugin.GetStatusCallCount.Should().Be(0);
    }

    /// <summary>
    /// A session of a still unpaid invoice that started 25 hours ago is not given up on: the
    /// reconciler keeps asking the gateway until it gives a final answer.
    /// </summary>
    [Fact]
    public async Task UnpaidSessionOlderThanADay_IsStillReconciled()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var host = BuildHost(resolver);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPendingInvoiceAsync(db, module, InvoiceStatus.Unpaid, DateTimeOffset.UtcNow.AddHours(-25));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>An Unpaid invoice with no gateway session at all is never picked up.</summary>
    [Fact]
    public async Task InvoiceWithNoGatewaySession_IsNotReconciled()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var host = BuildHost(resolver);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "No", "Session", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);
        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        plugin.GetStatusCallCount.Should().Be(0);
    }

    /// <summary>
    /// An administrator settles the invoice while its hosted session is open; the gateway later
    /// reports the session paid and the customer never returns. The reconciler, on a fixed clock an
    /// hour later, credits what the session took to the client's balance once; a second run credits
    /// nothing more.
    /// </summary>
    [Fact]
    public async Task SettledInvoiceWithLatePaidSession_IsCreditedOnce()
    {
        var module = $"reconcile-test-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentPluginResolver>(resolver);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));

        int invoiceId;
        int clientId;
        using (var seedScope = host.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = clock.GetUtcNow();
            var client = Client.Create($"subject-{Guid.NewGuid():N}", "Late", "Payer", $"{Guid.NewGuid():N}@example.test", now);
            db.Clients.Add(client);
            await db.SaveChangesAsync(CancellationToken.None);

            var invoice = Invoice.Create(client.Id, now.AddDays(7), "USD", now);
            invoice.AddItem("Hosting", 10m, 1);
            invoice.SetGatewaySession(module, $"gw-{Guid.NewGuid():N}", now);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(CancellationToken.None);

            // The administrator settles it while the session is still open.
            db.Transactions.Add(invoice.Settle(PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", now, TransactionSource.Manual)!);
            await db.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
            clientId = client.Id;
        }

        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        plugin.StatusResult = new(GatewayPaymentState.Paid, gatewayReference, "orderStatus:2", AmountMinor: 1000);
        clock.Advance(TimeSpan.FromHours(1));

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(10m);

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(10m);
        using var checkScope = host.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await check.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == gatewayReference)).Should().Be(1);
        (await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }
}
