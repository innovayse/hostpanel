using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ReconcileGatewayPaymentsCronHandler"/> on a fixed clock: the growing back-off between two
/// asks about one pending session and its upper bound, a final answer that settles or concludes a
/// session once, the once-per-session staff alert for a payment pending too long, a gateway call that
/// fails without stopping the pass, and the limit of the sweep over an already settled invoice.
/// </summary>
/// <remarks>
/// Each test mints its own module id, so a leftover invoice of another test (the database is shared)
/// resolves to no plugin and never counts against this test's plugin.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReconcileGatewayPaymentsBackoffTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test hosts.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>Builds a host on a fixed clock whose payment plugins come from <paramref name="resolver"/> and whose mail goes to <paramref name="sender"/>.</summary>
    /// <param name="resolver">The fake resolver the host resolves plugins through.</param>
    /// <param name="sender">The fake relay staff mail goes through.</param>
    /// <param name="clock">The clock the host reads.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver, FakeEmailSender sender, ManualTimeProvider clock) =>
        EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentPluginResolver>(resolver);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));

    /// <summary>Seeds a client and one invoice with a gateway session started at <paramref name="startedAt"/>.</summary>
    /// <param name="host">The test host whose database to seed.</param>
    /// <param name="module">This test's own plugin module id.</param>
    /// <param name="startedAt">When the session started, on the host's clock.</param>
    /// <param name="checks">How many times the reconciler already asked about the session, all at <paramref name="startedAt"/>.</param>
    /// <param name="settled">Whether another path settled the invoice while the session was open.</param>
    /// <returns>The seeded invoice's id.</returns>
    private static async Task<int> SeedSessionAsync(
        WebApplicationFactory<Program> host, string module, DateTimeOffset startedAt, int checks = 0, bool settled = false)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Gateway", "Payer", $"{Guid.NewGuid():N}@example.test", startedAt);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, startedAt.AddDays(7), "USD", startedAt);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.SetGatewaySession(module, $"gw-{Guid.NewGuid():N}", startedAt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        for (var i = 0; i < checks; i++)
        {
            invoice.RecordGatewayCheck(startedAt, concluded: false);
        }

        if (settled)
        {
            db.Transactions.Add(invoice.Settle(PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", startedAt, TransactionSource.Manual)!);
        }

        await db.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Runs one pass of the reconciler and waits for every message it published.</summary>
    /// <param name="host">The test host.</param>
    /// <returns>A task.</returns>
    private static async Task RunPassAsync(WebApplicationFactory<Program> host)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new ReconcileGatewayPaymentsCronCommand());
    }

    /// <summary>Reads an invoice as the database holds it.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice, untracked.</returns>
    private static async Task<Invoice> ReadAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices
            .AsNoTracking().SingleAsync(i => i.Id == invoiceId, CancellationToken.None);
    }

    /// <summary>
    /// A pending session is asked about at once, then again after 5 minutes, 10, 20: each wait is
    /// double the last, and a pass that comes sooner asks nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PendingSession_IsAskedAgainAfterAGrowingBackoffAsync()
    {
        var module = $"backoff-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        await SeedSessionAsync(host, module, clock.GetUtcNow().AddMinutes(-30));

        var waits = new[] { (TimeSpan.Zero, 1), (TimeSpan.FromMinutes(4), 1), (TimeSpan.FromMinutes(1), 2), (TimeSpan.FromMinutes(9), 2), (TimeSpan.FromMinutes(1), 3), (TimeSpan.FromMinutes(19), 3), (TimeSpan.FromMinutes(1), 4) };
        foreach (var (wait, expectedAsks) in waits)
        {
            clock.Advance(wait);
            await RunPassAsync(host);
            plugin.GetStatusCallCount.Should().Be(expectedAsks, $"after waiting {wait}");
        }
    }

    /// <summary>The wait between two asks stops growing at the upper bound of six hours.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Backoff_StopsGrowingAtTheUpperBoundAsync()
    {
        var module = $"backoff-cap-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        await SeedSessionAsync(host, module, clock.GetUtcNow().AddMinutes(-30), checks: 12);

        // The seeded asks were made 30 minutes before the clock now stands, so this is 359 minutes after the last one.
        clock.Advance(TimeSpan.FromMinutes(329));
        await RunPassAsync(host);
        var beforeTheBound = plugin.GetStatusCallCount;

        clock.Advance(TimeSpan.FromMinutes(1));
        await RunPassAsync(host);

        beforeTheBound.Should().Be(0);
        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>A session pending for a day is still asked about, and staff are told once, not on every ask.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PaymentPendingForADay_KeepsBeingCheckedAndMailsStaffOnceAsync()
    {
        var module = $"stalled-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var sender = new FakeEmailSender();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, sender, clock);
        var invoiceId = await SeedSessionAsync(host, module, clock.GetUtcNow().AddHours(-25));

        await RunPassAsync(host);
        clock.Advance(TimeSpan.FromHours(7));
        await RunPassAsync(host);

        plugin.GetStatusCallCount.Should().Be(2);
        sender.Sent.Should().ContainSingle(m => m.To == AdminEmail && m.Subject.Contains($"#{invoiceId}", StringComparison.Ordinal));
        (await ReadAsync(host, invoiceId)).GatewayStalledNotifiedAt.Should().NotBeNull();
    }

    /// <summary>A payment still pending after less than the threshold mails nobody.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PaymentPendingForAnHour_MailsNobodyAsync()
    {
        var module = $"young-{Guid.NewGuid():N}";
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, new FakePaymentPlugin());
        var sender = new FakeEmailSender();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, sender, clock);
        var invoiceId = await SeedSessionAsync(host, module, clock.GetUtcNow().AddHours(-1));

        await RunPassAsync(host);

        sender.Sent.Should().NotContain(m => m.Subject.Contains($"#{invoiceId}", StringComparison.Ordinal));
        (await ReadAsync(host, invoiceId)).GatewayStalledNotifiedAt.Should().BeNull();
    }

    /// <summary>A session the gateway declined for good is concluded and never asked about again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DeclinedSession_IsConcludedAndNotAskedAgainAsync()
    {
        var module = $"declined-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:6") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        var invoiceId = await SeedSessionAsync(host, module, clock.GetUtcNow().AddMinutes(-30));

        await RunPassAsync(host);
        clock.Advance(TimeSpan.FromDays(2));
        await RunPassAsync(host);

        plugin.GetStatusCallCount.Should().Be(1);
        (await ReadAsync(host, invoiceId)).GatewayConcludedAt.Should().NotBeNull();
    }

    /// <summary>A final paid answer settles the invoice once; later passes do not ask or credit again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PaidSession_SettlesTheInvoiceExactlyOnceAsync()
    {
        var module = $"paid-{Guid.NewGuid():N}";
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        var invoiceId = await SeedSessionAsync(host, module, clock.GetUtcNow().AddMinutes(-30));

        await RunPassAsync(host);
        clock.Advance(TimeSpan.FromDays(1));
        await RunPassAsync(host);

        plugin.GetStatusCallCount.Should().Be(1);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await db.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
    }

    /// <summary>A gateway call that throws is logged and counted as an ask; the other sessions are still asked and the pass ends normally.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailingGatewayCall_DoesNotStopThePassAsync()
    {
        var failingModule = $"failing-{Guid.NewGuid():N}";
        var healthyModule = $"healthy-{Guid.NewGuid():N}";
        var failing = new FakePaymentPlugin { StatusThrows = new HttpRequestException("gateway unreachable") };
        var healthy = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(failingModule, failing);
        resolver.Register(healthyModule, healthy);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        var failingId = await SeedSessionAsync(host, failingModule, clock.GetUtcNow().AddMinutes(-30));
        await SeedSessionAsync(host, healthyModule, clock.GetUtcNow().AddMinutes(-30));

        await RunPassAsync(host);

        failing.GetStatusCallCount.Should().Be(1);
        healthy.GetStatusCallCount.Should().Be(1);
        (await ReadAsync(host, failingId)).GatewayCheckCount.Should().Be(1);
    }

    /// <summary>A settled invoice's leftover session is swept only for the first day; older ones are left alone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SettledInvoiceSessionOlderThanADay_IsNotSweptAsync()
    {
        var module = $"settled-old-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, new FakeEmailSender(), clock);
        await SeedSessionAsync(host, module, clock.GetUtcNow().AddHours(-25), settled: true);

        await RunPassAsync(host);

        plugin.GetStatusCallCount.Should().Be(0);
    }
}
