using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <see cref="IServerSelector.SelectAsync"/> over several modules, the choice a product
/// with no server group is provisioned by. The shared database holds other tests' servers, so these
/// use modules no other test seeds.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServerSelectorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A server on any of the asked modules can be chosen.</summary>
    [Fact]
    public async Task SelectAsync_ChoosesAServerOnAnyOfTheModules()
    {
        using var scope = factory.Services.CreateScope();
        await SeedServerAsync(scope, ServerModule.Virtualmin);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var chosen = await selector.SelectAsync([ServerModule.HyperVm, ServerModule.Virtualmin], CancellationToken.None);

        // Not this test's own server by id: the other test seeds a Virtualmin server too, and two
        // empty servers tie, so either may be chosen. The module is what the selection decides.
        chosen.Should().NotBeNull();
        chosen!.Module.Should().Be(ServerModule.Virtualmin);
    }

    /// <summary>A server on a module that was not asked for is never chosen, however much room it has.</summary>
    [Fact]
    public async Task SelectAsync_NeverChoosesAServerOnAnotherModule()
    {
        using var scope = factory.Services.CreateScope();
        await SeedServerAsync(scope, ServerModule.Virtualmin);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var chosen = await selector.SelectAsync([ServerModule.HyperVm], CancellationToken.None);

        chosen.Should().BeNull();
    }

    /// <summary>Seeds an enabled server with room on the given module.</summary>
    /// <param name="scope">The scope whose data context seeds it.</param>
    /// <param name="module">The server's module.</param>
    /// <returns>The saved server.</returns>
    private static async Task<Server> SeedServerAsync(IServiceScope scope, ServerModule module)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = Server.Create(new ServerDetails(
            Name: $"srv-{Guid.NewGuid():N}",
            Hostname: "srv.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: module,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, "hash"),
            UseSSL: true,
            MaxAccounts: null,
            IsDefault: false,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), DateTimeOffset.UtcNow);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);
        return server;
    }
}
