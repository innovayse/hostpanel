using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <c>POST /api/me/services/{id}/upgrade-trial</c>: the owner converts their own running trial,
/// and a stranger is refused with <c>SERVICE_NOT_FOUND</c> before anything converts.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpgradeMyTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner's running trial is converted: awaiting payment, one invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_TheCallersRunningTrial_AwaitsPaymentWithOneInvoice()
    {
        var (owner, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "upgrade-trial-own");
        var serviceId = await SeedTrialServiceAsync(factory, clientId, trial: true);

        var response = await owner.PostAsync($"/api/me/services/{serviceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ServiceAsync(serviceId)).TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        (await InvoiceCountAsync(clientId)).Should().Be(1);
    }

    /// <summary>Another client's trial id is refused as not found and nothing converts or is invoiced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_AnotherClientsTrial_Returns404AndConvertsNothing()
    {
        var (_, ownerId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "upgrade-trial-victim");
        var serviceId = await SeedTrialServiceAsync(factory, ownerId, trial: true);
        var stranger = await ServicesTestSeeds.RegisterStrangerAsync(factory);

        var response = await stranger.PostAsync($"/api/me/services/{serviceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        (await ServiceAsync(serviceId)).TrialStatus.Should().Be(TrialStatus.Active);
        (await InvoiceCountAsync(ownerId)).Should().Be(0);
    }

    /// <summary>An ordinary service of the caller is refused with <c>SERVICE_NOT_IN_TRIAL</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_AnOrdinaryService_Returns409WithServiceNotInTrial()
    {
        var (owner, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "upgrade-trial-plain");
        var serviceId = await SeedTrialServiceAsync(factory, clientId, trial: false);

        var response = await owner.PostAsync($"/api/me/services/{serviceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
    }

    /// <summary>A suspended account is refused as starting a trial is, and nothing converts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_BySuspendedAccount_IsRefusedAndConvertsNothing()
    {
        var (owner, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "upgrade-trial-suspended");
        var serviceId = await SeedTrialServiceAsync(factory, clientId, trial: true);
        await ClientStatusSeeds.SetAsync(factory, clientId, ClientStatus.Suspended);

        var response = await owner.PostAsync($"/api/me/services/{serviceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_SUSPENDED);
        (await ServiceAsync(serviceId)).TrialStatus.Should().Be(TrialStatus.Active);
    }

    /// <summary>A user linked without <c>PlaceNewOrders</c> is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_ByAUserWithoutThePermission_Returns403()
    {
        var (_, ownerId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "upgrade-trial-owner");
        var serviceId = await SeedTrialServiceAsync(factory, ownerId, trial: true);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "upgrade-trial-denied", ownerId, ClientPermission.ViewEmails);

        var response = await user.PostAsync($"/api/me/services/{serviceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ServiceAsync(serviceId)).TrialStatus.Should().Be(TrialStatus.Active);
    }

    /// <summary>An anonymous caller is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Upgrade_ByAnonymous_Returns401()
    {
        var response = await factory.CreateClient().PostAsync($"/api/me/services/{ServicesTestSeeds.NoSuchServiceId}/upgrade-trial", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Seeds an active, activated service of a fresh priced product for the client: a running trial, or an ordinary service.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="trial">Whether the service is a running free trial.</param>
    /// <returns>The service id.</returns>
    internal static async Task<int> SeedTrialServiceAsync(IntegrationTestFactory factory, int clientId, bool trial)
    {
        using var scope = factory.Services.CreateScope();
        var ids = await ServicesTestSeeds.SeedClientAndProductAsync(scope, 10m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var rules = new ProductTrial(true, 14, true, [3, 1], 3);
        var service = ClientService.CreateOrdered(
            clientId, ids.ProductId, BillingCycle.Monthly, null, 0m, 10m, null, null, DateTimeOffset.UtcNow, trial: trial ? rules : null);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        service.ActivateDigital(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return service.Id;
    }

    /// <summary>Reads a service back.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The stored service.</returns>
    internal async Task<ClientService> ServiceAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId);
    }

    /// <summary>Counts the invoices of a client.</summary>
    /// <param name="clientId">The client id.</param>
    /// <returns>The number of invoices.</returns>
    private async Task<int> InvoiceCountAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.CountAsync(i => i.ClientId == clientId);
    }
}
