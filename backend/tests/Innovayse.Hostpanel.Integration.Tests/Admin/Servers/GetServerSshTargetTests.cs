using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshTarget;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><see cref="GetServerSshTargetQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared host.</param>
public sealed class GetServerSshTargetTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A configured server answers its module and complete agent settings.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ConfiguredServer_ReturnsModuleAndAgentSettingsAsync()
    {
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "target.example.test", 2200);

        var result = await InvokeAsync(serverId);

        result.IsSuccess.Should().BeTrue();
        var target = result.Value!;
        target.ServerId.Should().Be(serverId);
        target.Module.Should().Be(ServerModules.Linux);
        target.IsDisabled.Should().BeFalse();
        target.Agent.Should().NotBeNull();
        target.Agent!.IsEnabled.Should().BeTrue();
        target.Agent.IsComplete.Should().BeTrue();
        target.Agent.SshHostname.Should().Be("target.example.test");
        target.Agent.SshPort.Should().Be(2200);
    }

    /// <summary>A disabled SSH configuration is reported as such, not hidden.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DisabledSettings_ReturnsAgentNotEnabledAsync()
    {
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "off.example.test", 22, enabled: false);

        var result = await InvokeAsync(serverId);

        result.Value!.Agent!.IsEnabled.Should().BeFalse();
    }

    /// <summary>An unknown server fails with <c>SERVER_NOT_FOUND</c>.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnknownServer_FailsWithServerNotFoundAsync()
    {
        var result = await InvokeAsync(2147483000);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>Dispatches the query in a fresh scope.</summary>
    /// <param name="serverId">The server.</param>
    /// <returns>The query result.</returns>
    private async Task<Result<ServerSshTargetDto>> InvokeAsync(int serverId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<ServerSshTargetDto>>(new GetServerSshTargetQuery(serverId), CancellationToken.None);
    }
}
