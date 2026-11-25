using System.Net;
using System.Net.Http.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>POST /api/services/existing</c> against a hosting panel plugin built by the real activator and reached through the host's own
/// provisioning factory: the account is read from the server's panel before a service is recorded for it, a panel that has no such
/// account refuses, a panel that cannot read accounts needs the admin's confirmation, and the address the panel reports is kept.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddExistingServicePanelTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The panel holds the account: the service is stored Active, the panel was asked for that username and the address it reported is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddExisting_PanelHoldsTheAccount_StoresItWithTheReportedAddressAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        var (http, ids) = await AdminAsync(harness);

        var response = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "Panel_User", DomainOf()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var stored = await StoredAsync(await response.Content.ReadFromJsonAsync<int>());
        stored.Status.Should().Be(ServiceStatus.Active);
        stored.DedicatedIp.Should().Be("203.0.113.20");
        harness.Script.Received.OfType<PanelAccountContext>().Should().ContainSingle().Which.Service.ProvisioningRef.Should().Be("Panel_User");
    }

    /// <summary>The panel says it has no such account: 409 <c>SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER</c>, nothing is stored and the confirmation does not override it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddExisting_PanelHasNoSuchAccount_Returns409AndStoresNothingAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Account = ValueResult<PanelAccount, PanelRefusalReason>.NotFound();
        var (http, ids) = await AdminAsync(harness);
        var domain = DomainOf();

        var plain = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "ghost", domain));
        var confirmed = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "ghost", domain, confirmUnverified: true));

        foreach (var response in new[] { plain, confirmed })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER);
        }

        (await CountOnAsync(domain)).Should().Be(0);
    }

    /// <summary>A panel that cannot read accounts is answered 409 <c>SERVICE_HOSTING_ACCOUNT_UNVERIFIED</c> until the admin confirms; the confirmed service has no address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddExisting_PanelCannotReadAccounts_NeedsTheAdminsConfirmationAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeLoginlessPanelPlugin));
        var (http, ids) = await AdminAsync(harness);
        var domain = DomainOf();

        var refused = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "blind", domain));
        var confirmed = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "blind", domain, confirmUnverified: true));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(refused)).Should().Be(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_UNVERIFIED);
        confirmed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await StoredAsync(await confirmed.Content.ReadFromJsonAsync<int>())).DedicatedIp.Should().BeNull();
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>A panel plugin that throws is answered with the generic failure code and nothing of its text, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddExisting_PanelThrows_Returns502WithoutThePluginsTextAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Throw = new InvalidOperationException("plugin internal text");
        var (http, ids) = await AdminAsync(harness);
        var domain = DomainOf();

        var response = await http.PostAsJsonAsync("/api/services/existing", BodyOf(ids, harness.Server.Id, "broken", domain));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("plugin internal text");
        (await CountOnAsync(domain)).Should().Be(0);
    }

    /// <summary>A plugin that is switched off answers 503 <c>PANEL_PLUGIN_UNAVAILABLE</c> and the admin's confirmation does not stand in for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddExisting_PluginSwitchedOff_Returns503Async()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin), enabled: false);
        var (http, ids) = await AdminAsync(harness);

        var response = await http.PostAsJsonAsync(
            "/api/services/existing", BodyOf(ids, harness.Server.Id, "off", DomainOf(), confirmUnverified: true));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>Builds an admin client of a host that provisions through the harness's factory, and seeds a client and a product.</summary>
    /// <param name="harness">The installed plugin and its stored server.</param>
    /// <returns>The admin client and the seeded ids.</returns>
    private async Task<(HttpClient Http, ClientProductIds Ids)> AdminAsync(PanelPluginHarness harness)
    {
        using var scope = factory.Services.CreateScope();
        var ids = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 5m);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IProvisioningProviderFactory>(harness.Factory())));
        return (await AuthTestSeeds.AdminClientAsync(factory, host), ids);
    }

    /// <summary>Builds the request body of the add existing service call.</summary>
    /// <param name="ids">The seeded client and product.</param>
    /// <param name="serverId">The server the account lives on.</param>
    /// <param name="username">The account's username.</param>
    /// <param name="domain">The account's domain.</param>
    /// <param name="confirmUnverified">Whether the admin confirms an account the panel cannot be asked about.</param>
    /// <returns>The body, camelCase on the wire.</returns>
    private static AddExistingServiceCommand BodyOf(ClientProductIds ids, int serverId, string username, string domain, bool confirmUnverified = false)
    {
        return new AddExistingServiceCommand(
            ids.ClientId, ids.ProductId, BillingCycle.Monthly, 5m, serverId, domain, username, null, ConfirmUnverified: confirmUnverified);
    }

    /// <summary>A domain no other test of this host uses.</summary>
    /// <returns>A unique domain.</returns>
    private static string DomainOf() => $"{Guid.NewGuid():N}.example";

    /// <summary>Reads one stored service.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The stored service.</returns>
    private async Task<ClientService> StoredAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
    }

    /// <summary>Counts the stored services on a domain.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>How many services hold it.</returns>
    private async Task<int> CountOnAsync(string domain)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ClientServices.CountAsync(s => s.Domain == domain);
    }
}
