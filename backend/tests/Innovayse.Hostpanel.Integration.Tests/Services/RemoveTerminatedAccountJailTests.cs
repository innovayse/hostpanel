using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedAccountJail;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Once the panel deleted a terminated service's hosting account, the SSH jail the key agent kept for it is removed
/// through the agent (op <c>remove-jail</c>); the agent's own refusal while the account exists is never an error.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveTerminatedAccountJailTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A termination the panel carried out (Updated) asks the agent to remove the account's jail.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TerminatedOnThePanel_RemovesTheAccountsJailAsync()
    {
        var (_, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "jail-terminated");
        await using var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, new FakeProvisioningProvider());

        await host.Services.PublishAndWaitAsync(new ServiceTerminatedEvent(serviceId, clientId, "test", TestMoments.Seeded));

        var agents = (FakeSshKeyProviderFactory)host.Services.GetRequiredService<ISshKeyProviderFactory>();
        agents.AgentFor(serverId).RemovedJails.Should().ContainSingle().Which.Should().Be("web");
    }

    /// <summary>A panel that failed to delete the account leaves the jail alone.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TerminationFailedOnThePanel_LeavesTheJailAsync()
    {
        var (_, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "jail-terminate-failed");
        await using var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, new FakeProvisioningProvider { ShouldFail = true });

        await host.Services.PublishAndWaitAsync(new ServiceTerminatedEvent(serviceId, clientId, "test", TestMoments.Seeded));

        var agents = (FakeSshKeyProviderFactory)host.Services.GetRequiredService<ISshKeyProviderFactory>();
        agents.AgentFor(serverId).RemovedJails.Should().BeEmpty();
    }

    /// <summary>The agent refusing (the account still exists, or an agent older than 1.4.0) is logged, not a failure.</summary>
    /// <param name="failure">The agent's refusal.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(SshProviderFailure.AccountExists)]
    [InlineData(SshProviderFailure.UnknownOperation)]
    public async Task AgentRefuses_CommandStillSucceedsAsync(SshProviderFailure failure)
    {
        var (_, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, $"jail-refused-{(int)failure}");
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        agent.RemoveJailFailure = failure;

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RemoveTerminatedAccountJailCommand(serviceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        agent.RemovedJails.Should().BeEmpty();
    }
}
