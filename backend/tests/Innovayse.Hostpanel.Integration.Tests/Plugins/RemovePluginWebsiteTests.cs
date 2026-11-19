using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostHostingApi.RemoveWebsiteAsync"/> through <c>PluginHostingService</c>: a plugin removes only its
/// own websites of the caller's service, the removal is idempotent, a failed removal is recorded and can be retried,
/// taking a website away does not need the product to still be bound, and every refusal comes before any agent call.
/// </summary>
/// <param name="fixture">The host with the hosting plugins.</param>
public sealed class RemovePluginWebsiteTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>The plugin's own website is taken off the server, recorded as removed and audited.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnWebsite_IsRemovedFromTheServerAndRecordedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-ok", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-ok.example.test");

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).WebsiteOf("web", "app.rmsite-ok.example.test").Should().BeNull();
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .Contain($"Plugin {fixture.HostingPluginId} removed website app.rmsite-ok.example.test from service #{owner.ServiceId}");
    }

    /// <summary>Removing twice succeeds twice and the server is asked once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovingTwice_IsIdempotentAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-twice", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-twice.example.test");

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-website").Should().Be(1);
    }

    /// <summary>After a removal the host name is free again: the same plugin can set it up as a new website.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovedHostname_CanBeSetUpAgainAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-again", fixture.HostingPluginId);
        var first = await EnsureAsync(owner, "app.rmsite-again.example.test");
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, first.Id);

        var second = await EnsureAsync(owner, "app.rmsite-again.example.test");

        second.Id.Should().NotBe(first.Id);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().HaveCount(2);
    }

    /// <summary>Another plugin's website, a missing one and another service's answer NotFound, and nothing is removed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NotTheCallersOwnWebsite_IsNotFoundAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-foreign", fixture.HostingPluginId, fixture.OtherHostingPluginId);
        var other = await fixture.OwnerWithDomainBoundToAsync("rmsite-foreign-b", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-foreign.example.test");
        var otherSite = await EnsureAsync(other, "app.rmsite-foreign-b.example.test");

        var anotherPlugin = await RefusalAsync(fixture.OtherHostingPluginId, owner.ClientId, owner.ServiceId, website.Id);
        var missing = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, Guid.NewGuid());
        var wrongService = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, otherSite.Id);
        var foreignService = await RefusalAsync(fixture.HostingPluginId, other.ClientId, owner.ServiceId, website.Id);

        new[] { anotherPlugin, missing, wrongService, foreignService }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotFound);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-website").Should().Be(0);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>The gate's refusals, each with no agent call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GateRefusals_AreAnsweredWithNoAgentCallAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-gate", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-gate.example.test");
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);

        var databasesOnly = await RefusalAsync(fixture.DatabasesPluginId, owner.ClientId, owner.ServiceId, website.Id);
        var plain = await RefusalAsync(fixture.PlainPluginId, owner.ClientId, owner.ServiceId, website.Id);
        var admin = await fixture.AsHostingPluginAsync(PluginCallBinding.WithoutClient(fixture.HostingPluginId), async api =>
        {
            var act = () => api.RemoveWebsiteAsync(owner.ServiceId, website.Id, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
        var noRight = await fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(
                fixture.HostingPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords),
            async api =>
            {
                var act = () => api.RemoveWebsiteAsync(owner.ServiceId, website.Id, CancellationToken.None);
                return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
            });

        new[] { databasesOnly, plain, admin, noRight }.Should().OnlyContain(f => f == PluginHostAccessFailure.NotAllowed);
        agent.CallsOf("remove-website").Should().Be(0);
    }

    /// <summary>Taking a website away does not need the product to still be bound to the plugin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProductNoLongerBound_StillAllowsTheRemovalAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-unbound", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-unbound.example.test");
        await fixture.BindProductsOfAsync(fixture.HostingPluginId);

        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        fixture.HostingAgents().AgentFor(owner.ServerId).WebsiteOf("web", "app.rmsite-unbound.example.test").Should().BeNull();
    }

    /// <summary>A server that fails the removal is ServerUnavailable, the record says it failed, and a retry removes it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FailedRemoval_IsRecordedAndCanBeRetriedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-fail", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-fail.example.test");
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);
        agent.FailWith = SshProviderFailure.Unreachable;

        var failure = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.RemoveFailed);

        agent.FailWith = null;
        await RemoveAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Removed);
        agent.WebsiteOf("web", "app.rmsite-fail.example.test").Should().BeNull();
    }

    /// <summary>A website whose service moved to another account is never removed through the new one: Unavailable, no agent call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceMovedToAnotherAccount_IsUnavailableAndNothingIsSentAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-moved", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-moved.example.test");
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            fixture.Factory,
            owner.ServiceId,
            service => service.RecordHostingAccount(
                new ServiceHostingAccount(service.ServerId, service.Domain, "someoneelse", null, service.Username, null)));

        var failure = await RefusalAsync(fixture.HostingPluginId, owner.ClientId, owner.ServiceId, website.Id);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        fixture.HostingAgents().AgentFor(owner.ServerId).CallsOf("remove-website").Should().Be(0);
    }

    /// <summary>A spent budget refuses the removal before the server is called, and the record stays active.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpentBudget_RefusesTheRemovalAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("rmsite-budget", fixture.HostingPluginId);
        var website = await EnsureAsync(owner, "app.rmsite-budget.example.test");
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);
        using var budgeted = fixture.HostWithHostingBudget(0);

        var failure = await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).RemoveWebsiteAsync(owner.ServiceId, website.Id, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        PluginHostAccessFixture.HostingAgentsOf(budgeted).AgentFor(owner.ServerId).CallsOf("remove-website").Should().Be(0);
        (await fixture.WebsitesOfAsync(owner.ServiceId)).Should().ContainSingle().Which.State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>Sets up a website of the hosting plugin with the usual specification.</summary>
    /// <param name="owner">The service's owner scenario.</param>
    /// <param name="hostname">The host name.</param>
    /// <returns>The website.</returns>
    private Task<PluginWebsite> EnsureAsync(SshOwnerScenario owner, string hostname)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId),
            api => api.EnsureWebsiteAsync(owner.ServiceId, new PluginWebsiteSpec(hostname, "sites/app", "8.3", true), CancellationToken.None));
    }

    /// <summary>Removes a website as a plugin does.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="websiteId">The website.</param>
    /// <returns>A task that completes once it is removed.</returns>
    private Task RemoveAsync(string pluginId, int clientId, int serviceId, Guid websiteId)
    {
        return fixture.AsHostingPluginAsync(
            PluginHostAccessFixture.ClientCall(pluginId, clientId),
            async api =>
            {
                await api.RemoveWebsiteAsync(serviceId, websiteId, CancellationToken.None);
                return true;
            });
    }

    /// <summary>Removes a website and returns the refusal the plugin catches.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="websiteId">The website.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(string pluginId, int clientId, int serviceId, Guid websiteId)
    {
        return await fixture.AsHostingPluginAsync(PluginHostAccessFixture.ClientCall(pluginId, clientId), async api =>
        {
            var act = () => api.RemoveWebsiteAsync(serviceId, websiteId, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
    }
}
