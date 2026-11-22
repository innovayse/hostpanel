using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostServiceConnectionApi.ResolveServiceConnectionAsync"/> through the host a plugin call is handed:
/// each refusal in its order, the stored credentials handed over only for the caller's own, active, operator-bound service
/// and only once, the activity line written for a grant and for nothing else, and no secret in any log line or printed value (#1357).
/// </summary>
/// <param name="fixture">The host with the service connection plugins.</param>
public sealed class ResolvePluginServiceConnectionTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>The panel password the seeded server stores.</summary>
    private const string StoredPassword = "panel-password-7f3a";

    /// <summary>The API token the seeded server stores.</summary>
    private const string StoredToken = "panel-token-91bc";

    /// <summary>The access hash the seeded server stores.</summary>
    private const string StoredHash = "panel-hash-44de";

    /// <summary>The credentials every seeded server stores.</summary>
    private static readonly ServerCredentials Credentials = new(StoredPassword, StoredToken, StoredHash);

    /// <summary>A bound, active service of the caller answers with its server, every stored credential, the SSL flag and the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BoundActiveService_AnswersWithItsServerAndAccountAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-ok", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);

        var context = await GrantedAsync(fixture.ConnectionPluginId, owner);

        context.Server.ServerId.Should().Be(owner.ServerId);
        context.Server.Hostname.Should().Be("conn-ok.example.test");
        context.Server.UseSsl.Should().BeTrue();
        context.Server.Username.Should().Be("root");
        context.Server.Password.Should().Be(StoredPassword);
        context.Server.ApiToken.Should().Be(StoredToken);
        context.Server.AccessHash.Should().Be(StoredHash);
        context.Server.Port.Should().BeNull();
        context.Server.AccountId.Should().BeNull();
        context.Service.ServiceId.Should().Be(owner.ServiceId);
        context.Service.ProvisioningRef.Should().Be("web");
    }

    /// <summary>A plugin whose manifest does not declare the kind gets nothing, even for a bound product.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginWithoutTheDeclaredKind_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-undeclared", Credentials);
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var failure = await RefusalAsync(fixture.SshPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should().BeEmpty();
    }

    /// <summary>A contact without the passwords right is refused as the portal would refuse to show a password.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ContactWithoutThePasswordsRight_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-contact", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);
        var contact = PluginHostAccessFixture.ClientCall(
            fixture.ConnectionPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords);

        var failure = await RefusalAsync(contact, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should().BeEmpty();
    }

    /// <summary>An administrator call carries no client, so it gets nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCall_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-admin", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);

        var failure = await RefusalAsync(PluginCallBinding.WithoutClient(fixture.ConnectionPluginId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }

    /// <summary>
    /// Another client's service and a service that does not exist answer the same NotFound. The foreign service's
    /// product is the one bound to the plugin, so a NotAllowed would show the binding was asked before ownership.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForeignAndMissingService_AnswerTheSameNotFoundAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-own", Credentials);
        var other = await fixture.OwnerWithCredentialsAsync("conn-foreign", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, other.ServiceId);

        var foreign = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, other.ServiceId);
        var missing = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, int.MaxValue);

        foreign.Should().Be(PluginHostAccessFailure.NotFound);
        missing.Should().Be(foreign);
        (await fixture.ActivityOfAsync(other.ClientId)).Should().BeEmpty();
    }

    /// <summary>A product the operator did not bind to the plugin is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProductNotBoundByTheOperator_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-unbound", Credentials);

        var failure = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }

    /// <summary>A manifest that lists every product does not count: only the operator's binding does.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManifestListingEveryProduct_DoesNotCountAsABindingAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-greedy", Credentials);

        var failure = await RefusalAsync(fixture.GreedyConnectionPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }

    /// <summary>The binding of another plugin does not open the service to this one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherPluginsBinding_DoesNotCountAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-other-plugin", Credentials);
        await fixture.BindProductsOfAsync(fixture.OtherConnectionPluginId, owner.ServiceId);

        var failure = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }

    /// <summary>A suspended and a terminated service have no usable account: Unavailable.</summary>
    /// <param name="terminate"><see langword="true"/> to terminate the service, <see langword="false"/> to suspend it.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedOrTerminatedService_IsUnavailableAsync(bool terminate)
    {
        var owner = await fixture.OwnerWithCredentialsAsync($"conn-inactive-{terminate}", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            fixture.Factory,
            owner.ServiceId,
            service =>
            {
                if (terminate)
                {
                    service.Terminate(TestMoments.Seeded, hostingAlreadyUpdated: true);
                }
                else
                {
                    service.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true);
                }
            });

        var failure = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should().BeEmpty();
    }

    /// <summary>A service with no server, one without a provisioning reference and one whose server is gone are Unavailable.</summary>
    /// <param name="damage">What is taken away from the service.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ServiceDamage.NoServer)]
    [InlineData(ServiceDamage.NoProvisioningReference)]
    [InlineData(ServiceDamage.DeletedServer)]
    public async Task ServiceWithoutAnAccountOnAServer_IsUnavailableAsync(ServiceDamage damage)
    {
        var owner = await fixture.OwnerWithCredentialsAsync($"conn-damaged-{damage}", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);
        await DamageAsync(owner.ServiceId, owner.ServerId, damage);

        var failure = await RefusalAsync(fixture.ConnectionPluginId, owner.ClientId, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
    }

    /// <summary>A plugin the operator switched off is refused before any credential leaves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginSwitchedOff_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-off", Credentials);
        await fixture.BindProductsOfAsync(fixture.OtherConnectionPluginId, owner.ServiceId);
        await fixture.SwitchAsync(fixture.OtherConnectionPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(fixture.OtherConnectionPluginId, owner.ClientId, owner.ServiceId);

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            (await fixture.ActivityOfAsync(owner.ClientId)).Should().BeEmpty();
        }
        finally
        {
            await fixture.SwitchAsync(fixture.OtherConnectionPluginId, enabled: true);
        }
    }

    /// <summary>A grant writes one activity line with the plugin and the service and no credential, a refusal writes none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryGrant_IsAuditedOnceWithoutACredentialAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-audit", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);

        await GrantedAsync(fixture.ConnectionPluginId, owner);

        var lines = await fixture.ActivityOfAsync(owner.ClientId);
        lines.Should().ContainSingle()
            .Which.Should().Be($"Plugin {fixture.ConnectionPluginId} read the server connection of service #{owner.ServiceId}");
        lines.Should().NotContain(line => line.Contains(StoredPassword) || line.Contains(StoredToken) || line.Contains(StoredHash));
    }

    /// <summary>A grant whose activity line cannot be saved hands the plugin nothing: a read that is not recorded is not given.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ActivityLineThatCannotBeSaved_GivesThePluginNoCredentialsAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-audit-fails", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);
        using var failing = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new FailingSaveInterceptor(StagesActivityLine)))));
        var binding = PluginHostAccessFixture.ClientCall(fixture.ConnectionPluginId, owner.ClientId);
        PanelAccountContext? handed = null;

        var act = async () =>
        {
            handed = await PluginHostAccessFixture.AsPluginAsync(
                failing,
                binding,
                api => ((IPluginHostServiceConnectionApi)api).ResolveServiceConnectionAsync(owner.ServiceId, CancellationToken.None));
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
        handed.Should().BeNull();
        (await fixture.ActivityOfAsync(owner.ClientId)).Should().BeEmpty();
    }

    /// <summary>No credential reaches a log line, a printed value or an outcome's text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Credentials_AreInNoLogLineAndNoPrintedValueAsync()
    {
        var owner = await fixture.OwnerWithCredentialsAsync("conn-secret", Credentials);
        await fixture.BindProductsOfAsync(fixture.ConnectionPluginId, owner.ServiceId);

        var context = await GrantedAsync(fixture.ConnectionPluginId, owner);

        var printed = new[] { context.ToString(), context.Server.ToString() };
        foreach (var secret in new[] { StoredPassword, StoredToken, StoredHash })
        {
            printed.Should().NotContain(text => text.Contains(secret));
            fixture.LoggedMessages.Should().NotContain(line => line.Contains(secret));
        }

        (await fixture.DatabaseHoldsAsync(StoredPassword)).Should().BeFalse("the password lives only in its own encrypted column, never in an audit or message row");
    }

    /// <summary>Tells whether a pending change set adds an activity line, the save the test makes fail.</summary>
    /// <param name="tracker">The change tracker of the save.</param>
    /// <returns><see langword="true"/> when the save must be refused.</returns>
    private static bool StagesActivityLine(ChangeTracker tracker)
    {
        return tracker.Entries<ActivityLog>().Any(entry => entry.State == EntityState.Added);
    }

    /// <summary>Takes something away from a seeded service the way an operator or a failed provisioning leaves it.</summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="serverId">Its server.</param>
    /// <param name="damage">What to take away.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    private async Task DamageAsync(int serviceId, int serverId, ServiceDamage damage)
    {
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        switch (damage)
        {
            case ServiceDamage.NoServer:
                await context.ClientServices.Where(s => s.Id == serviceId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ServerId, (int?)null));
                break;
            case ServiceDamage.NoProvisioningReference:
                await context.ClientServices.Where(s => s.Id == serviceId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ProvisioningRef, (string?)null));
                break;
            default:
                await context.Servers.Where(s => s.Id == serverId).ExecuteDeleteAsync();
                break;
        }
    }

    /// <summary>Resolves the connection as a plugin call of the owner with every permission.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="owner">The owner whose service is asked about.</param>
    /// <returns>What the host granted.</returns>
    private Task<PanelAccountContext> GrantedAsync(string pluginId, SshOwnerScenario owner)
    {
        return fixture.AsConnectionPluginAsync(
            PluginHostAccessFixture.ClientCall(pluginId, owner.ClientId),
            api => api.ResolveServiceConnectionAsync(owner.ServiceId, CancellationToken.None));
    }

    /// <summary>The refusal a call of the client with every permission is answered with.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <returns>The reason the host gave.</returns>
    private Task<PluginHostAccessFailure> RefusalAsync(string pluginId, int clientId, int serviceId)
    {
        return RefusalAsync(PluginHostAccessFixture.ClientCall(pluginId, clientId), serviceId);
    }

    /// <summary>The refusal a call is answered with.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <returns>The reason the host gave.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId)
    {
        var act = () => fixture.AsConnectionPluginAsync(
            binding, api => api.ResolveServiceConnectionAsync(serviceId, CancellationToken.None));
        var thrown = await act.Should().ThrowAsync<PluginHostAccessException>();
        return thrown.Which.Failure;
    }
}
