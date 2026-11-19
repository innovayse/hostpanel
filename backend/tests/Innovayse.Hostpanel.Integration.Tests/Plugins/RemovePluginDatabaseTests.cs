using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostHostingApi.RemoveDatabaseAsync"/> through <c>PluginHostingService</c>: a plugin drops only its own
/// databases of the caller's service, the removal is idempotent, a failed removal is recorded and can be retried, taking
/// a database away does not need the product to still be bound, and every refusal comes before any agent call.
/// </summary>
/// <param name="fixture">The host with the hosting plugins.</param>
public sealed class RemovePluginDatabaseTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>The plugin's own database is dropped from the server by its label, recorded as removed and audited.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnDatabase_IsDroppedFromTheServerAndRecordedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-ok", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).HasDatabase("web", "shop").Should().BeFalse();
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .Contain($"Plugin {fixture.HostingPluginId} removed database web_shop from service #{owner.ServiceId}");
    }

    /// <summary>Removing twice succeeds twice and the server is asked once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovingTwice_IsIdempotentAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-twice", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-database").Should().Be(1);
    }

    /// <summary>After a drop the label is free again: the same plugin gets a new database and a new password.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DroppedLabel_CanBeCreatedAgainWithANewPasswordAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-again", fixture.HostingPluginId);
        var first = await EnsureAsync(owner, "shop");
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, first.Id);

        var second = await EnsureAsync(owner, "shop");

        second.Id.Should().NotBe(first.Id);
        second.Password.Should().Be(FakeHostingAgent.DatabasePassword);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().HaveCount(2);
    }

    /// <summary>Another plugin's database, a missing one and another service's answer NotFound, and nothing is dropped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NotTheCallersOwnDatabase_IsNotFoundAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-foreign", fixture.HostingPluginId, fixture.OtherHostingPluginId);
        var other = await fixture.OwnerWithDomainBoundToAsync("rmdb-foreign-b", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");
        var otherDatabase = await EnsureAsync(other, "shop");

        var anotherPlugin = await RefusalAsync(fixture.OtherHostingPluginId, owner.ClientId, owner.ServiceId, database.Id);
        var missing = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, Guid.NewGuid());
        var wrongService = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, otherDatabase.Id);
        var foreignService = await RefusalAsync(fixture.HostingPluginId, other.ClientId, owner.ServiceId, database.Id);

        new[] { anotherPlugin, missing, wrongService, foreignService }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotFound);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-database").Should().Be(0);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>The gate's refusals, each with no agent call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GateRefusals_AreAnsweredWithNoAgentCallAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-gate", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");

        var websitesOnly = await RefusalAsync(fixture.WebsitesPluginId, owner.ClientId, owner.ServiceId, database.Id);
        var plain = await RefusalAsync(fixture.PlainPluginId, owner.ClientId, owner.ServiceId, database.Id);
        var admin = await fixture.AsHostingPluginAsync(PluginCallBinding.WithoutClient(fixture.HostingPluginId), async api =>
        {
            var act = () => api.RemoveDatabaseAsync(owner.ServiceId, database.Id, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
        var noRight = await fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(
                fixture.HostingPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords),
            async api =>
            {
                var act = () => api.RemoveDatabaseAsync(owner.ServiceId, database.Id, CancellationToken.None);
                return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
            });

        new[] { websitesOnly, plain, admin, noRight }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotAllowed);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-database").Should().Be(0);
    }

    /// <summary>Taking a database away does not need the product to still be bound to the plugin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProductNoLongerBound_StillAllowsTheRemovalAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-unbound", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");
        await fixture.BindProductsOfAsync(fixture.HostingPluginId);

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).HasDatabase("web", "shop").Should().BeFalse();
    }

    /// <summary>A server that fails the removal is ServerUnavailable, the record says it failed, and a retry drops it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FailedRemoval_IsRecordedAndCanBeRetriedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-fail", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.FailWith = SshProviderFailure.Unreachable;

        var failure = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.RemoveFailed);

        agent.FailWith = null;
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        agent.HasDatabase("web", "shop").Should().BeFalse();
    }

    /// <summary>A database whose service moved to another account is never dropped through the new one: Unavailable, no agent call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceMovedToAnotherAccount_IsUnavailableAndNothingIsSentAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-moved", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            fixture.Factory,
            owner.ServiceId,
            service => service.RecordHostingAccount(
                new ServiceHostingAccount(service.ServerId, service.Domain, "someoneelse", null, service.Username, null)));

        var failure = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, database.Id);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-database").Should().Be(0);
    }

    /// <summary>A spent budget refuses the removal before the server is called, and the record stays active.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpentBudget_RefusesTheRemovalAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmdb-budget", fixture.HostingPluginId);
        var database = await EnsureAsync(owner, "shop");
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);
        using var budgeted = fixture.HostWithHostingBudget(0);

        var failure = await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).RemoveDatabaseAsync(owner.ServiceId, database.Id, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        PluginHostAccessFixture.HostingAgentsOf(budgeted).AgentFor(owner.ServerId).CallsOf("remove-database").Should().Be(0);
        (await fixture.DatabasesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>Creates a database of the hosting plugin.</summary>
    /// <param name="owner">The service's owner scenario.</param>
    /// <param name="label">The label.</param>
    /// <returns>The database.</returns>
    private Task<PluginDatabase> EnsureAsync(SshOwnerScenario owner, string label)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId),
            api => api.EnsureDatabaseAsync(owner.ServiceId, label, CancellationToken.None));
    }

    /// <summary>Removes a database as a plugin does.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="databaseId">The database.</param>
    /// <returns>A task that completes once it is removed.</returns>
    private Task RemoveAsync(string pluginId, int clientId, int serviceId, Guid databaseId)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(pluginId, clientId),
            async api =>
            {
                await api.RemoveDatabaseAsync(serviceId, databaseId, CancellationToken.None);
                return true;
            });
    }

    /// <summary>Removes a database and returns the refusal the plugin catches.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="databaseId">The database.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(string pluginId, int clientId, int serviceId, Guid databaseId)
    {
        return await fixture.AsHostingPluginAsync(PluginHostAccessFixture.ClientCall(pluginId, clientId), async api =>
        {
            var act = () => api.RemoveDatabaseAsync(serviceId, databaseId, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
    }
}
