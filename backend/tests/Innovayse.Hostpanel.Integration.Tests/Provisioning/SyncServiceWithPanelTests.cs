using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Integration tests for <c>POST /api/admin/services/{id}/sync-panel</c>: the account is read from the panel once, compared with the
/// service, and the platform's status, package and (on request) password are applied to the panel through the operations that
/// already exist, while the panel's address is recorded on the service. Nothing the panel reports changes the service's own status.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncServiceWithPanelTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The package the seeded product names.</summary>
    private const string ProductPackage = "starter";

    /// <summary>An account that matches the service on everything the panel reports is in step: nothing is sent to the panel and only the address is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_InStep_SendsNothingToThePanelAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), "STARTER", "203.0.113.9");
        var before = Snapshot(provider);

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Status").Should().Be("InSync");
        OutcomeOf(fields, "Package").Should().Be("InSync");
        OutcomeOf(fields, "Domain").Should().Be("InSync");
        OutcomeOf(fields, "IpAddress").Should().Be("Applied");
        fields.Select(f => f.GetProperty("field").GetString()).Should().NotContain("Password");
        Snapshot(provider).Should().Be(before);
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("203.0.113.9");
    }

    /// <summary>A panel that reports the account suspended while the service is active is unsuspended, and the service stays as it is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelSuspendedServiceActive_UnsuspendsThePanelAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Suspended, DomainOf(serviceId), ProductPackage, null);

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Status").Should().Be("Applied");
        provider.UnsuspendCalls.Should().Be(1);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A panel that reports the account active while the service is suspended is suspended, and the service stays suspended.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelActiveServiceSuspended_SuspendsThePanelAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        await SuspendInPlaceAsync(serviceId);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), ProductPackage, null);

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Status").Should().Be("Applied");
        provider.SuspendCalls.Should().Be(1);
        (await ReloadAsync(serviceId)).Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>A package other than the product's is changed on the panel to the product's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_OtherPackage_ChangesThePackageToTheProductsAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), "legacy", null);

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Package").Should().Be("Applied");
        PanelValueOf(fields, "Package").Should().Be("legacy");
        provider.ChangedPackages.Should().Equal(ProductPackage);
    }

    /// <summary>The panel's address is recorded on the service, replacing the one it held, and is shown as applied.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelReportsAnAddress_RecordsItAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), ProductPackage, "198.51.100.77");

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "IpAddress").Should().Be("Applied");
        PanelValueOf(fields, "IpAddress").Should().Be("198.51.100.77");
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("198.51.100.77");
    }

    /// <summary>An address the panel reports that is no address is not stored and the one the service holds stays.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelReportsNoUsableAddress_KeepsTheStoredOneAsync()
    {
        var provider = new FakeProvisioningProvider { AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, null, null, "203.0.113.9") };
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), ProductPackage, "not-an-address");

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "IpAddress").Should().Be("Unsupported");
        PanelValueOf(fields, "IpAddress").Should().BeNull();
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("203.0.113.9");
    }

    /// <summary>The stored password reaches the panel only when staff ask for it.</summary>
    /// <param name="pushPassword">Whether staff ticked the box.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_Password_IsPushedOnlyWhenAskedAsync(bool pushPassword)
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), ProductPackage, null);

        var fields = await SyncAsync(http, serviceId, pushPassword);

        provider.ChangePasswordCalls.Should().Be(pushPassword ? 1 : 0);
        if (pushPassword)
        {
            OutcomeOf(fields, "Password").Should().Be("Applied");
            PanelValueOf(fields, "Password").Should().BeNull();
        }
        else
        {
            fields.Select(f => f.GetProperty("field").GetString()).Should().NotContain("Password");
        }
    }

    /// <summary>A stored password the password command would refuse is reported as failed, not as an error of the whole sync.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_StoredPasswordTooShort_IsReportedFailedAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        await StorePasswordAsync(serviceId, "short");
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, DomainOf(serviceId), ProductPackage, null);

        var fields = await SyncAsync(http, serviceId, pushPassword: true);

        OutcomeOf(fields, "Password").Should().Be("Failed");
        provider.ChangePasswordCalls.Should().Be(0);
    }

    /// <summary>A domain that differs is reported with the panel's value and nothing is done about it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_DomainDiffers_IsReportedAndLeftAloneAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Active, "moved.example.test", ProductPackage, null);
        var before = Snapshot(provider);

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Domain").Should().Be("Unsupported");
        PanelValueOf(fields, "Domain").Should().Be("moved.example.test");
        Snapshot(provider).Should().Be(before);
        (await ReloadAsync(serviceId)).Domain.Should().Be(DomainOf(serviceId));
    }

    /// <summary>A change the panel refuses is reported in its field and the sync still answers 200 with the rest.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelRefusesTheChange_IsReportedFailedAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.AccountAnswer = new HostedAccountState(HostedAccountStatus.Suspended, DomainOf(serviceId), "legacy", "198.51.100.78");
        provider.FailChanges = true;

        var fields = await SyncAsync(http, serviceId, pushPassword: false);

        OutcomeOf(fields, "Status").Should().Be("Failed");
        OutcomeOf(fields, "Package").Should().Be("Failed");
        OutcomeOf(fields, "IpAddress").Should().Be("Applied");
        (await ReloadAsync(serviceId)).DedicatedIp.Should().Be("198.51.100.78");
    }

    /// <summary>A panel that cannot be read answers 502 <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> and none of its detail reaches the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelCannotBeRead_Returns502WithServicePanelSyncUpstreamFailedAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.FailAccountRead = true;

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("fake provider");
    }

    /// <summary>A panel that cannot read one account answers its own code, not the generic failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_PanelCannotReadAnAccount_Returns409WithThePanelsCodeAsync()
    {
        var provider = new FakeProvisioningProvider();
        var (http, serviceId) = await ProvisionedAsync(provider);
        provider.FailWith = new DomainRuleException(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "no single account read");

        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED);
    }

    /// <summary>A service that was never provisioned has no account to read and is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_NeverProvisioned_Returns400WithServiceNotProvisionedAsync()
    {
        var seedHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, seedHttp, await factory.GetAdminTokenAsync());

        var response = await seedHttp.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_PROVISIONED);
    }

    /// <summary>An id no service takes answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_UnknownService_Returns404WithServiceNotFoundAsync()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.PostAsJsonAsync(
            $"/api/admin/services/{ProvisioningTestSeeds.NoSuchServiceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_NonPositiveId_Returns400ValidationErrorAsync()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var response = await http.PostAsJsonAsync("/api/admin/services/0/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Without a session the route answers 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_Returns401_WithoutAuthAsync()
    {
        var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync(
            $"/api/admin/services/{ProvisioningTestSeeds.NoSuchServiceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client of the portal is refused with 403: the route is staff only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Sync_Client_Returns403Async()
    {
        var http = factory.CreateClient();
        var owned = await ProvisioningTestSeeds.SeedOwnedServiceAsync(factory, http, await factory.GetAdminTokenAsync());
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owned.OwnerToken);

        var response = await http.PostAsJsonAsync($"/api/admin/services/{owned.ServiceId}/sync-panel", new { pushPassword = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Seeds a service and provisions it through the fake provider.</summary>
    /// <param name="provider">The fake the host provisions through.</param>
    /// <returns>An admin client of the host that uses the fake, and the provisioned service's id.</returns>
    private async Task<(HttpClient Http, int ServiceId)> ProvisionedAsync(FakeProvisioningProvider provider)
    {
        var seedHttp = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, seedHttp, await factory.GetAdminTokenAsync());
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);
        (await http.PostAsync($"/api/provisioning/{serviceId}/provision", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (http, serviceId);
    }

    /// <summary>Runs the sync and reads the per field answer.</summary>
    /// <param name="http">An admin client.</param>
    /// <param name="serviceId">The service to sync.</param>
    /// <param name="pushPassword">Whether to push the stored password.</param>
    /// <returns>The <c>fields</c> of the answer.</returns>
    private static async Task<List<JsonElement>> SyncAsync(HttpClient http, int serviceId, bool pushPassword)
    {
        var response = await http.PostAsJsonAsync($"/api/admin/services/{serviceId}/sync-panel", new { pushPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return [.. body.GetProperty("fields").EnumerateArray()];
    }

    /// <summary>Reads the outcome of one field.</summary>
    /// <param name="fields">The answer's fields.</param>
    /// <param name="field">The field's name.</param>
    /// <returns>The outcome's name.</returns>
    private static string? OutcomeOf(List<JsonElement> fields, string field)
    {
        return fields.Single(f => f.GetProperty("field").GetString() == field).GetProperty("outcome").GetString();
    }

    /// <summary>Reads the panel value of one field.</summary>
    /// <param name="fields">The answer's fields.</param>
    /// <param name="field">The field's name.</param>
    /// <returns>The value the panel reported, or <see langword="null"/>.</returns>
    private static string? PanelValueOf(List<JsonElement> fields, string field)
    {
        return fields.Single(f => f.GetProperty("field").GetString() == field).GetProperty("panelValue").GetString();
    }

    /// <summary>Counts every call that changes the panel, so a test can prove none was made.</summary>
    /// <param name="provider">The fake provider.</param>
    /// <returns>The suspend, unsuspend, package and password counts.</returns>
    private static (int Suspend, int Unsuspend, int Package, int Password) Snapshot(FakeProvisioningProvider provider)
    {
        return (provider.SuspendCalls, provider.UnsuspendCalls, provider.ChangePackageCalls, provider.ChangePasswordCalls);
    }

    /// <summary>The domain the seed stored on a service.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The domain.</returns>
    private static string DomainOf(int serviceId)
    {
        return $"prov{serviceId}.example.com";
    }

    /// <summary>Suspends a service in the database, as its own status, without any panel call.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>A task that completes when the service is suspended.</returns>
    private async Task SuspendInPlaceAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId);
        service.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Stores another password on a service, as staff would correct it.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <param name="password">The password to store.</param>
    /// <returns>A task that completes when the password is stored.</returns>
    private async Task StorePasswordAsync(int serviceId, string password)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == serviceId);
        service.RecordHostingAccount(
            new ServiceHostingAccount(service.ServerId, service.Domain, service.ProvisioningRef, service.DedicatedIp, service.Username, password));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads a service back from the database in a fresh scope.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The stored service.</returns>
    private async Task<ClientService> ReloadAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);
    }
}
