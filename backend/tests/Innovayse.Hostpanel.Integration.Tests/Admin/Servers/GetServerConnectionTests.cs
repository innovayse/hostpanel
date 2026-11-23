using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerConnection;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><see cref="GetServerConnectionQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared host.</param>
public sealed class GetServerConnectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A stored server answers its id, module and hostname.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task KnownServer_ReturnsItsConnectionAsync()
    {
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "connection.example.test", 22);

        var result = await InvokeAsync(serverId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(serverId);
        result.Value.Module.Should().Be(ServerModules.Linux);
        result.Value.Hostname.Should().NotBeNullOrWhiteSpace();
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
    private async Task<Result<ServerConnectionDto>> InvokeAsync(int serverId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<ServerConnectionDto>>(new GetServerConnectionQuery(serverId), CancellationToken.None);
    }
}
