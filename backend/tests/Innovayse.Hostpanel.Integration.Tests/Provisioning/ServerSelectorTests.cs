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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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

    /// <summary>A fill until full group fills the first server by name, then the next once it is full.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelectFromGroupAsync_FillUntilFull_ChoosesTheFirstByNameThenTheNext()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = await SeedGroupAsync(context, ServerFillType.FillUntilFull);
        var first = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 10, accounts: 8, name: $"a-{Guid.NewGuid():N}");
        var second = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 10, accounts: 0, name: $"b-{Guid.NewGuid():N}");
        first.AssignToGroup(group.Id);
        second.AssignToGroup(group.Id);
        await context.SaveChangesAsync(CancellationToken.None);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var before = await selector.SelectFromGroupAsync(group.Id, [ServerModule.Virtualmin], CancellationToken.None);
        first.RecordConnectionTest(true, 10, TestMoments.Seeded);
        await context.SaveChangesAsync(CancellationToken.None);
        var after = await selector.SelectFromGroupAsync(group.Id, [ServerModule.Virtualmin], CancellationToken.None);

        before!.Id.Should().Be(first.Id);
        after!.Id.Should().Be(second.Id);
    }

    /// <summary>A least full group picks the emptier server whatever its name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelectFromGroupAsync_LeastFull_ChoosesTheEmptier()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = await SeedGroupAsync(context, ServerFillType.LeastFull);
        var busy = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 10, accounts: 8, name: $"a-{Guid.NewGuid():N}");
        var quiet = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 10, accounts: 1, name: $"b-{Guid.NewGuid():N}");
        busy.AssignToGroup(group.Id);
        quiet.AssignToGroup(group.Id);
        await context.SaveChangesAsync(CancellationToken.None);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var chosen = await selector.SelectFromGroupAsync(group.Id, [ServerModule.Virtualmin], CancellationToken.None);

        chosen!.Id.Should().Be(quiet.Id);
    }

    /// <summary>An unknown group and a group whose servers are all full give no server.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelectFromGroupAsync_ReturnsNullForAnUnknownOrFullGroup()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = await SeedGroupAsync(context, ServerFillType.FillUntilFull);
        var full = await SeedServerAsync(scope, ServerModule.Virtualmin, maxAccounts: 3, accounts: 3);
        full.AssignToGroup(group.Id);
        await context.SaveChangesAsync(CancellationToken.None);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var unknown = await selector.SelectFromGroupAsync(int.MaxValue, [ServerModule.Virtualmin], CancellationToken.None);
        var allFull = await selector.SelectFromGroupAsync(group.Id, [ServerModule.Virtualmin], CancellationToken.None);

        unknown.Should().BeNull();
        allFull.Should().BeNull();
    }

    /// <summary>With no group the default server wins a tie on fill and capacity.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelectAsync_PrefersTheDefaultOnATie()
    {
        using var scope = factory.Services.CreateScope();
        // Centos is a module no other test seeds, so the only candidates are these two.
        await SeedServerAsync(scope, ServerModule.Centos, maxAccounts: 10, accounts: 1, name: $"a-{Guid.NewGuid():N}");
        var preferred = await SeedServerAsync(scope, ServerModule.Centos, maxAccounts: 10, accounts: 1, name: $"b-{Guid.NewGuid():N}", isDefault: true);
        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();

        var chosen = await selector.SelectAsync([ServerModule.Centos], CancellationToken.None);

        chosen!.Id.Should().Be(preferred.Id);
    }

    /// <summary>Saves a new server group with the given fill type.</summary>
    /// <param name="context">The data context that saves it.</param>
    /// <param name="fillType">How the group fills its servers.</param>
    /// <returns>The saved group.</returns>
    private static async Task<ServerGroup> SeedGroupAsync(IHostpanelDbContext context, ServerFillType fillType)
    {
        var group = ServerGroup.Create($"grp-{Guid.NewGuid():N}", fillType);
        context.ServerGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        return group;
    }

    /// <summary>Seeds an enabled server with room on the given module.</summary>
    /// <param name="scope">The scope whose data context seeds it.</param>
    /// <param name="module">The server's module.</param>
    /// <param name="maxAccounts">Its capacity, or null for unlimited.</param>
    /// <param name="accounts">How many accounts it already holds, or null when never counted.</param>
    /// <param name="name">Its name, or null for a random one.</param>
    /// <param name="isDefault">Whether it is the default server.</param>
    /// <returns>The saved server.</returns>
    private static async Task<Server> SeedServerAsync(IServiceScope scope, ServerModule module, int? maxAccounts = null, int? accounts = null, string? name = null, bool isDefault = false)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = Server.Create(new ServerDetails(
            Name: name ?? $"srv-{Guid.NewGuid():N}",
            Hostname: "srv.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: module,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, "hash"),
            UseSSL: true,
            MaxAccounts: maxAccounts,
            IsDefault: isDefault,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), TestMoments.Seeded);
        server.RecordConnectionTest(true, accounts, TestMoments.Seeded);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);
        return server;
    }
}
