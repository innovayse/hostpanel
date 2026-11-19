using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostHostingApi.GetHostingCapabilitiesAsync"/> through <c>PluginHostingService</c>: each refusal in its
/// order with no agent call, only the kinds the manifest declares reported, a service that cannot take sites answered
/// all false, the PHP versions of the server, a down server, and the spent budget failing closed.
/// </summary>
/// <param name="fixture">The host with the hosting plugins.</param>
public sealed class GetPluginHostingCapabilitiesTests(PluginHostAccessFixture fixture) : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>A plugin that declares websites and databases reads what the server reports, with the PHP versions it offers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeclaredKinds_ReportWhatTheServerCanDoAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-ok", fixture.HostingPluginId);

        var caps = await CapabilitiesAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId);

        caps.Should().BeEquivalentTo(new PluginHostingCapabilities(false, true, true, ["8.2", "8.3"]));
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().ContainSingle().Which.Should().StartWith("ping:");
    }

    /// <summary>A kind the manifest does not declare reads as false, and a server operation it lacks reads as false for the plugin that declares it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UndeclaredKindsAndMissingOperations_ReadAsFalseAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-partial", fixture.WebsitesPluginId);
        fixture.HostingAgents().AgentFor(owner.ServerId).Capabilities = new HostingCapabilities(true, false, ["8.3"]);

        var caps = await CapabilitiesAsync(PluginHostAccessFixture.ClientCall(fixture.WebsitesPluginId, owner.ClientId), owner.ServiceId);

        caps.Should().BeEquivalentTo(new PluginHostingCapabilities(false, true, false, ["8.3"]));
    }

    /// <summary>A plugin that declares only SSH reads whether SSH is available and never asks the hosting agent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SshOnlyPlugin_ReadsSshAndNeverAsksTheHostingAgentAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-ssh", fixture.SshPluginId);

        var caps = await CapabilitiesAsync(PluginHostAccessFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        caps.Should().BeEquivalentTo(new PluginHostingCapabilities(true, false, false, []));
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>A service whose server has SSH switched off answers all false, not a refusal, and the agent is never called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceThatCannotTakeSites_ReadsAllFalseAsync()
    {
        var owner = await fixture.OwnerOnSshDisabledServerAsync("caps-disabled");
        await fixture.BindProductsOfAsync(fixture.HostingPluginId, owner.ServiceId);

        var caps = await CapabilitiesAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId);

        caps.Should().BeEquivalentTo(new PluginHostingCapabilities(false, false, false, []));
        fixture.HostingAgents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>The gate's refusals, each answered before any agent is called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GateRefusals_AreAnsweredWithNoAgentCallAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-gate", fixture.HostingPluginId);
        var stranger = await fixture.OwnerWithServiceAsync("caps-gate-stranger");
        var unbound = await fixture.OwnerWithServiceAsync("caps-gate-unbound");
        var agent = fixture.HostingAgents().AgentFor(owner.ServerId);

        var undeclared = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.PlainPluginId, owner.ClientId), owner.ServiceId);
        var admin = await RefusalAsync(PluginCallBinding.WithoutClient(fixture.HostingPluginId), owner.ServiceId);
        var noRight = await RefusalAsync(
            PluginHostAccessFixture.ClientCall(
                fixture.HostingPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords),
            owner.ServiceId);
        var foreign = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, stranger.ClientId), owner.ServiceId);
        var missing = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), int.MaxValue);
        var notBound = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, unbound.ClientId), unbound.ServiceId);

        undeclared.Should().Be(PluginHostAccessFailure.NotAllowed);
        admin.Should().Be(PluginHostAccessFailure.NotAllowed);
        noRight.Should().Be(PluginHostAccessFailure.NotAllowed);
        foreign.Should().Be(PluginHostAccessFailure.NotFound);
        missing.Should().Be(PluginHostAccessFailure.NotFound);
        notBound.Should().Be(PluginHostAccessFailure.NotAllowed);
        agent.Calls.Should().BeEmpty();
    }

    /// <summary>A server that does not answer is ServerUnavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnreachableServer_IsServerUnavailableAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-down", fixture.HostingPluginId);
        fixture.HostingAgents().AgentFor(owner.ServerId).FailWith = SshProviderFailure.Unreachable;

        var failure = await RefusalAsync(PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
    }

    /// <summary>A spent budget refuses the read before the server is asked, and a limiter that cannot answer refuses too (fail closed).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpentBudgetAndDeadLimiter_RefuseBeforeTheServerIsAskedAsync()
    {
        var owner = await fixture.OwnerWithDomainBoundToAsync("caps-budget", fixture.HostingPluginId);
        var binding = PluginHostAccessFixture.ClientCall(fixture.HostingPluginId, owner.ClientId);
        using var budgeted = fixture.HostWithHostingBudget(1);
        await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, api => ((IPluginHostHostingApi)api).GetHostingCapabilitiesAsync(owner.ServiceId, CancellationToken.None));

        var spent = await PluginHostAccessFixture.AsPluginAsync(budgeted, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).GetHostingCapabilitiesAsync(owner.ServiceId, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
        using var dead = fixture.HostWithADeadLimiter();
        await PluginHostAccessFixture.AsPluginAsync(dead, binding, async api =>
        {
            var act = () => ((IPluginHostHostingApi)api).GetHostingCapabilitiesAsync(owner.ServiceId, CancellationToken.None);
            await act.Should().ThrowAsync<ObjectDisposedException>();
            return true;
        });

        spent.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        PluginHostAccessFixture.HostingAgentsOf(budgeted).AgentFor(owner.ServerId).Calls
            .Should().ContainSingle("only the first read reached the server");
        PluginHostAccessFixture.HostingAgentsOf(dead).AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>Reads the capabilities as a plugin does.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <returns>The capabilities.</returns>
    private Task<PluginHostingCapabilities> CapabilitiesAsync(PluginCallBinding binding, int serviceId)
    {
        return fixture.AsHostingPluginAsync(binding, api => api.GetHostingCapabilitiesAsync(serviceId, CancellationToken.None));
    }

    /// <summary>Reads the capabilities and returns the refusal the plugin catches.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId)
    {
        return await fixture.AsHostingPluginAsync(binding, async api =>
        {
            var act = () => api.GetHostingCapabilitiesAsync(serviceId, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure;
        });
    }
}
