using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Commands.AutoTerminateSuspendedCron;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="AutoTerminateSuspendedCronHandler"/> against Postgres: which suspended services its
/// overdue-only <c>SuspendedAt &lt;= threshold</c> selection picks up, that each is terminated through the
/// termination use case with one panel call, and which end-of-cycle services it terminates once their cycle ended.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AutoTerminateSuspendedCronTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the end-of-cycle tests run at.</summary>
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Seeds a client, a product and one suspended <see cref="ClientService"/>.</summary>
    /// <param name="db">The concrete data context.</param>
    /// <param name="suspendedAt">The moment the service is suspended at.</param>
    /// <param name="source">Why the service is suspended; non-payment unless a test says otherwise.</param>
    /// <returns>The seeded service.</returns>
    private static async Task<ClientService> SeedSuspendedServiceAsync(
        AppDbContext db, DateTimeOffset suspendedAt, SuspensionSource source = SuspensionSource.Overdue)
    {
        var clientId = await SeedClientAsync(db);
        var productId = await SeedProductAsync(db);

        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(db, service, "panel-ref", suspendedAt);
        service.Suspend(suspendedAt, "test", source: source);
        await db.SaveChangesAsync(CancellationToken.None);

        return service;
    }

    /// <summary>Seeds a client to own a service.</summary>
    /// <param name="db">The concrete data context.</param>
    /// <returns>The seeded client's id.</returns>
    private static async Task<int> SeedClientAsync(AppDbContext db)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        return client.Id;
    }

    /// <summary>Seeds a product to hang a service on.</summary>
    /// <param name="db">The concrete data context.</param>
    /// <returns>The seeded product's id.</returns>
    private static async Task<int> SeedProductAsync(AppDbContext db)
    {
        var group = ProductGroup.Create($"auto-terminate-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);

        return product.Id;
    }

    /// <summary>A service staff suspended by hand is never terminated by this job, however long ago.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualSuspensionPastTheLimit_IsNotTerminated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await SeedSuspendedServiceAsync(db, DateTimeOffset.UtcNow.AddDays(-30), SuspensionSource.Manual);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        var reloaded = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>A service imported already suspended is never terminated by this job, however long ago.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ImportedSuspensionPastTheLimit_IsNotTerminated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await SeedClientAsync(db);
        var productId = await SeedProductAsync(db);
        var longAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var service = ClientService.Import(new ServiceImport(
            clientId, productId, BillingCycle.Monthly, ServiceStatus.Suspended, longAgo, longAgo, SuspensionSource.Other,
            new ServiceHostingAccount(null, "imported.example.test", "imported", null, "imported", null)));
        db.ClientServices.Add(service);
        await db.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        var reloaded = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>A service suspended well past the fourteen-day limit is terminated.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceSuspendedPastTheLimit_IsTerminated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await SeedSuspendedServiceAsync(db, DateTimeOffset.UtcNow.AddDays(-15));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        var reloaded = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Terminated);
    }

    /// <summary>
    /// A long-suspended service on a server is terminated through the termination use case: the panel is
    /// asked exactly once, with no made-up reason, and a second pass asks it nothing more.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ServiceSuspendedPastTheLimit_CallsThePanelOnceAsync()
    {
        var provider = new RecordingProvisioningProvider();
        await using var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        int serviceId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            serviceId = (await ProvisioningTestSeeds.SeedActiveServiceAsync(context)).ServiceId;
            var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId, CancellationToken.None);
            service.Suspend(DateTimeOffset.UtcNow.AddDays(-15), hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new AutoTerminateSuspendedCronCommand());
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new AutoTerminateSuspendedCronCommand());

        using var check = host.Services.CreateScope();
        var reloaded = await check.ServiceProvider.GetRequiredService<AppDbContext>().ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Terminated);
        var call = provider.Terminated.Should().ContainSingle(c => c.ServiceId == serviceId).Subject;
        call.Reason.Should().BeEmpty();
    }

    /// <summary>A service suspended only recently is left alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceSuspendedRecently_IsNotTerminated()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await SeedSuspendedServiceAsync(db, DateTimeOffset.UtcNow.AddDays(-1));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        var reloaded = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>An Active service is never picked up by the suspended-only filter, however old.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ActiveService_IsNeverTerminatedByThisCron()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        var group = ProductGroup.Create($"auto-terminate-active-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(db, service, "panel-ref", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        var reloaded = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// The threshold is read from the host clock: a service suspended a day ago is left alone today
    /// and terminated once the clock stands past the fourteen-day limit.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceSuspendedRecently_IsTerminatedOnceTheClockPassesTheLimit()
    {
        using var host = new ClockedHost(factory);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await SeedSuspendedServiceAsync(db, host.Clock.GetUtcNow().AddDays(-1));
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
        var today = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);

        host.Clock.Advance(TimeSpan.FromDays(14));
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
        var later = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);

        today.Status.Should().Be(ServiceStatus.Suspended);
        later.Status.Should().Be(ServiceStatus.Terminated);
    }

    /// <summary>
    /// A service set to terminate at the end of its cycle whose renewal date has passed is terminated,
    /// and its open end-of-period cancellation request is closed by the termination's own handler.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task EndOfCycleServicePastItsRenewalDate_IsTerminatedAndItsRequestClosed()
    {
        using var host = new ClockedHost(factory, FixedNow);
        int serviceId;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            serviceId = await SeedEndOfCycleServiceAsync(db, FixedNow.AddMonths(-2));
            db.CancellationRequests.Add(CancellationRequest.Create(serviceId, CancellationType.EndOfBillingPeriod, "Moving on"));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new AutoTerminateSuspendedCronCommand());

        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Terminated);
        (await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.ServiceId == serviceId, CancellationToken.None))
            .Status.Should().Be(CancellationStatus.Closed);
    }

    /// <summary>A service set to terminate at the end of a cycle that has not ended yet is left running.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task EndOfCycleServiceBeforeItsRenewalDate_IsNotTerminated()
    {
        using var host = new ClockedHost(factory, FixedNow);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var serviceId = await SeedEndOfCycleServiceAsync(db, FixedNow.AddDays(-1));
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);

        (await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>
    /// A service already terminated is not selected again: a second pass leaves its termination
    /// moment as the first pass wrote it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task EndOfCycleServiceAlreadyTerminated_IsSkippedOnTheNextPass()
    {
        using var host = new ClockedHost(factory, FixedNow);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var serviceId = await SeedEndOfCycleServiceAsync(db, FixedNow.AddMonths(-2));
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
        var first = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);

        host.Clock.Advance(TimeSpan.FromDays(1));
        await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
        var second = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);

        first.Status.Should().Be(ServiceStatus.Terminated);
        second.Status.Should().Be(ServiceStatus.Terminated);
        first.TerminatedAt.Should().Be(FixedNow);
        second.TerminatedAt.Should().Be(FixedNow);
    }

    /// <summary>
    /// The delay before a long suspension is terminated is read from the billing setting: a service
    /// suspended fifteen days ago is left alone while the setting says thirty and terminated once it says ten.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task TheDelayIsReadFromTheBillingSetting()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await SeedSuspendedServiceAsync(db, DateTimeOffset.UtcNow.AddDays(-15));
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var setting = await db.Settings.FirstAsync(s => s.Key == BillingSettingKeys.TerminateAfterSuspendedDays, CancellationToken.None);

        try
        {
            setting.UpdateValue("30");
            await db.SaveChangesAsync(CancellationToken.None);
            await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
            var withLongDelay = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);

            setting.UpdateValue("10");
            await db.SaveChangesAsync(CancellationToken.None);
            await bus.InvokeAsync(new AutoTerminateSuspendedCronCommand(), CancellationToken.None);
            var withShortDelay = await db.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);

            withLongDelay.Status.Should().Be(ServiceStatus.Suspended);
            withShortDelay.Status.Should().Be(ServiceStatus.Terminated);
        }
        finally
        {
            setting.UpdateValue("14");
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A panel that refuses the removal leaves the service suspended and mails staff; once the panel
    /// answers again, the next run terminates the same service.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task APanelThatRefuses_KeepsTheServiceRetryableAndMailsStaffAsync()
    {
        const string adminEmail = "ops@example.test";
        var provider = new FakeProvisioningProvider { ShouldFail = true };
        var sender = new FakeEmailSender();
        await using var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: adminEmail)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IProvisioningProviderFactory>(new FakeProvisioningProviderFactory(provider));
                services.AddSingleton<IProvisioningProvider>(provider);
            }));
        int serviceId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            serviceId = (await ProvisioningTestSeeds.SeedActiveServiceAsync(context)).ServiceId;
            var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId, CancellationToken.None);
            service.Suspend(DateTimeOffset.UtcNow.AddDays(-15), hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new AutoTerminateSuspendedCronCommand());

        using (var check = host.Services.CreateScope())
        {
            (await check.ServiceProvider.GetRequiredService<AppDbContext>().ClientServices.AsNoTracking()
                .FirstAsync(s => s.Id == serviceId, CancellationToken.None))
                .Status.Should().Be(ServiceStatus.Suspended);
        }

        sender.Sent.Should().ContainSingle(m => m.To == adminEmail && m.Subject.Contains($"#{serviceId}", StringComparison.Ordinal));

        provider.ShouldFail = false;
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new AutoTerminateSuspendedCronCommand());

        using var retried = host.Services.CreateScope();
        (await retried.ServiceProvider.GetRequiredService<AppDbContext>().ClientServices.AsNoTracking()
            .FirstAsync(s => s.Id == serviceId, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Terminated);
    }

    /// <summary>Seeds an active monthly service set to terminate at the end of its cycle.</summary>
    /// <param name="db">The concrete data context.</param>
    /// <param name="activatedAt">The billing anchor; the renewal date is one month later.</param>
    /// <returns>The seeded service's id.</returns>
    private static async Task<int> SeedEndOfCycleServiceAsync(AppDbContext db, DateTimeOffset activatedAt)
    {
        var clientId = await SeedClientAsync(db);
        var productId = await SeedProductAsync(db);

        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, activatedAt);
        await ServicesTestSeeds.AddActivatedAsync(db, service, "panel-ref", activatedAt);
        service.ConfigureAutomation(true, false, null, autoTerminateEndOfCycle: true, "Moving on");
        await db.SaveChangesAsync(CancellationToken.None);

        return service.Id;
    }
}
