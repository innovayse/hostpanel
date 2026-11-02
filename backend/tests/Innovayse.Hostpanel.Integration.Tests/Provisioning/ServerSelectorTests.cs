using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <see cref="IServerSelector.SelectAsync"/> over several modules, the choice a product
/// with no server group is provisioned by. The shared database holds other tests' servers, so these
/// use modules no other test seeds; each test here keeps to its own (HyperVm is never seeded, so it
/// stands for a module with no server).
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

    /// <summary>Only modules that some enabled server is on are reported as in use.</summary>
    [Fact]
    public async Task ModulesInUseAsync_ListsOnlyModulesWithAnEnabledServer()
    {
        using var scope = factory.Services.CreateScope();
        await SeedServerAsync(scope, ServerModule.Virtualmin);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var inUse = await selector.ModulesInUseAsync([ServerModule.HyperVm, ServerModule.Virtualmin], CancellationToken.None);

        inUse.Should().Equal(ServerModule.Virtualmin);
    }

    /// <summary>A group member on a module that was not asked for is skipped, even when it is the emptier one.</summary>
    [Fact]
    public async Task SelectFromGroupAsync_SkipsAMemberOnAnotherModule()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ServerGroup.Create($"grp-{Guid.NewGuid():N}", ServerFillType.LeastFull);
        context.ServerGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var supported = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 10, accounts: 9);
        var unsupported = await SeedServerAsync(scope, ServerModule.Lukittu);
        supported.AssignToGroup(group.Id);
        unsupported.AssignToGroup(group.Id);
        await context.SaveChangesAsync(CancellationToken.None);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var chosen = await selector.SelectFromGroupAsync(group.Id, [ServerModule.Virtualmin], CancellationToken.None);

        chosen.Should().NotBeNull();
        chosen!.Id.Should().Be(supported.Id);
    }

    /// <summary>Seeds an enabled server with room on the given module.</summary>
    /// <param name="scope">The scope whose data context seeds it.</param>
    /// <param name="module">The server's module.</param>
    /// <param name="maxAccounts">Its capacity, or null for unlimited.</param>
    /// <param name="accounts">How many accounts it already holds, or null when never counted.</param>
    /// <returns>The saved server.</returns>
    private static async Task<Server> SeedServerAsync(IServiceScope scope, ServerModule module, int? maxAccounts = null, int? accounts = null)
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
            MaxAccounts: maxAccounts,
            IsDefault: false,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), DateTimeOffset.UtcNow);
        server.RecordConnectionTest(true, accounts, DateTimeOffset.UtcNow);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);
        return server;
    }
}
