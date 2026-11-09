using Innovayse.Hostpanel.Application.Services.Queries.IsServerInUse;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="IsServerInUseQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class IsServerInUseTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A server no service or SSH key points at is not in use.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnreferencedServer_IsNotInUseAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var serverId = await ServersTestSeeds.CreateServerAsync(client, $"Server-{Guid.NewGuid():N}");

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<bool>>(new IsServerInUseQuery(serverId));

        result.Value.Should().BeFalse();
    }
}
