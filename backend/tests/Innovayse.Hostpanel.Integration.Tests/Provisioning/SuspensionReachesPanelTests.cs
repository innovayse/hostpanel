using Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// A suspension made in the platform reaches the hosting control panel, end to end: from the
/// command (or the overdue cron that sends it) through the saved aggregate's domain event to the
/// provisioning provider. The handler tests beside this one publish the event by hand; these do
/// not, so they also prove the event is raised and dispatched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SuspensionReachesPanelTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Suspending a service suspends its hosting account once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SuspendService_SuspendsTheHostingAccount()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        int serviceId;
        using (var scope = host.Services.CreateScope())
        {
            (_, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(
                scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new SuspendServiceCommand(serviceId, SuspensionSource.Overdue));

        provider.Suspended.Should().ContainSingle().Which.ProvisioningRef.Should().Be("provisioning-ref-1");
    }

    /// <summary>Unsuspending a suspended service unsuspends its hosting account once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnsuspendService_UnsuspendsTheHostingAccount()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        int serviceId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (_, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);
            var service = await context.ClientServices.FindAsync(serviceId);
            service!.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new UnsuspendServiceCommand(serviceId));

        provider.Unsuspended.Should().ContainSingle().Which.Should().Be("provisioning-ref-1");
    }

    /// <summary>
    /// The overdue cron's suspension reaches the panel: a service on an invoice overdue past the
    /// grace period is suspended in the platform and on the server.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OverdueCron_SuspendsTheHostingAccount()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);
            var due = TestMoments.Seeded.AddDays(-10);
            var invoice = Invoice.Create(clientId, due, "USD", TestMoments.Seeded);
            invoice.AddItem("Hosting", 25m, 1, serviceId);
            invoice.UpdateOptions(due.AddDays(-14), due, null, 0m);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoice.MarkOverdue(TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new SuspendOverdueServicesCronCommand());

        provider.Suspended.Should().ContainSingle().Which.ProvisioningRef.Should().Be("provisioning-ref-1");
    }
}
