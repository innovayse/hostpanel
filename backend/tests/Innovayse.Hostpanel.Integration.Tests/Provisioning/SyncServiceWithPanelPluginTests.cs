using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <c>POST /api/admin/services/{id}/sync-panel</c> against a hosting panel plugin built by the real activator and reached through the
/// host's own provisioning factory: the plugin is read once, each difference is sent to it through the operation that already exists, and
/// a plugin that cannot read an account, throws or is switched off is answered with the host's own code and none of its text.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncServiceWithPanelPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The reference the seeded service holds on the plugin's panel.</summary>
    private const string AccountReference = "acct-sync";

    /// <summary>Every difference is sent to the plugin: the account is read once, then unsuspended, moved to the product's package and given the stored password, and the address is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PluginReportsDifferences_SendsEachThroughItsOwnOperationAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Account = ValueResult<PanelAccount, PanelRefusalReason>.Done(
            new PanelAccount(AccountReference, "one.example.test", PanelAccountStatus.Suspended, "legacy", null) { IpAddress = "203.0.113.20" });
        var (http, serviceId) = await ActiveServiceAsync(harness);

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fields = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fields").EnumerateArray().ToList();
        OutcomeOf(fields, "Status").Should().Be("Applied");
        OutcomeOf(fields, "Package").Should().Be("Applied");
        OutcomeOf(fields, "Domain").Should().Be("Unsupported");
        OutcomeOf(fields, "IpAddress").Should().Be("Applied");
        OutcomeOf(fields, "Password").Should().Be("Applied");
        harness.Script.Received.Select(c => c.GetType()).Should().Equal(
            typeof(PanelAccountContext), typeof(PanelAccountContext), typeof(PanelChangePackageContext), typeof(PanelChangePasswordContext));
        harness.Script.Received.OfType<PanelAccountContext>().Should().OnlyContain(c => c.Service.ProvisioningRef == AccountReference);
        harness.Script.Received.OfType<PanelChangePackageContext>().Single().Package.Name.Should().Be("starter");
        harness.Script.Received.OfType<PanelChangePasswordContext>().Single().NewPassword.Should().Be("Passw0rd!");
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
        stored.DedicatedIp.Should().Be("203.0.113.20");
        stored.Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A plugin that does not declare the single account read answers <c>PANEL_OPERATION_NOT_SUPPORTED</c> and is never called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PluginWithoutTheCapability_Returns409AndIsNotCalledAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeLoginlessPanelPlugin));
        var (http, serviceId) = await ActiveServiceAsync(harness);

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
        harness.Script.Received.Should().BeEmpty();
    }

    /// <summary>A plugin that throws is answered with the generic failure code and nothing of its text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PluginThrows_Returns502WithoutThePluginsTextAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin));
        harness.Script.Throw = new InvalidOperationException("plugin internal text");
        var (http, serviceId) = await ActiveServiceAsync(harness);

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("plugin internal text");
    }

    /// <summary>A plugin that is switched off answers 503 <c>PANEL_PLUGIN_UNAVAILABLE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PluginSwitchedOff_Returns503Async()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakeAccountPanelPlugin), enabled: false);
        var (http, serviceId) = await ActiveServiceAsync(harness);

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>Seeds a service, activates it on the harness's server and builds an admin client of a host that provisions through the harness's factory.</summary>
    /// <param name="harness">The installed plugin and its stored server.</param>
    /// <returns>The admin client and the active service's id.</returns>
    private async Task<(HttpClient Http, int ServiceId)> ActiveServiceAsync(PanelPluginHarness harness)
    {
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, factory.CreateClient(), await factory.GetAdminTokenAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId);
            service.ActivateOn(harness.Server.Id, AccountReference, TestMoments.Seeded, TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IProvisioningProviderFactory>(harness.Factory())));
        return (await AuthTestSeeds.AdminClientAsync(factory, host), serviceId);
    }

    /// <summary>Reads the outcome of one field.</summary>
    /// <param name="fields">The answer's fields.</param>
    /// <param name="field">The field's name.</param>
    /// <returns>The outcome's name.</returns>
    private static string? OutcomeOf(List<JsonElement> fields, string field)
    {
        return fields.Single(f => f.GetProperty("field").GetString() == field).GetProperty("outcome").GetString();
    }
}
