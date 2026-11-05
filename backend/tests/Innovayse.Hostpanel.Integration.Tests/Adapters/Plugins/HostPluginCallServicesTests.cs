using System.Reflection;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="HostPluginCallServices"/> against Postgres: the production container builds it, one binding gives the
/// host facts and the store scope of the same client (or of none), the store scope is the binding's own reference and is
/// never derived again, each call gets its own quota signal, and no method a plugin can call takes an id.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class HostPluginCallServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A fixed moment the seeded rows are dated from, so no row depends on the wall clock.</summary>
    private static readonly DateTimeOffset Origin = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The production container resolves the host implementation, not a stub.</summary>
    [Fact]
    public void Container_ResolvesHostPluginCallServices()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Should().BeOfType<HostPluginCallServices>();
    }

    /// <summary>
    /// A binding with a client stores under exactly the binding's reference, not one derived again from the client id,
    /// and lists that client's services.
    /// </summary>
    [Fact]
    public async Task BindingWithAClient_StoresUnderTheBindingsReference_AndListsItsServicesAsync()
    {
        var pluginId = $"host-bound-{Guid.NewGuid():N}";
        var (clientId, serviceId) = await SeedClientWithServiceAsync();

        // A reference no derivation would produce: if the factory derived the scope itself, the row would carry another one.
        var givenRef = $"given-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>()
            .Create(PluginCallBinding.ForClient(pluginId, clientId, givenRef, ClientPermission.All));
        await services.Store.SetAsync("greeting", "hello", CancellationToken.None);
        var listed = await services.Host.ListServicesAsync(CancellationToken.None);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.PluginDataEntries.AsNoTracking().SingleAsync(d => d.PluginId == pluginId);
        row.ClientRef.Should().Be(givenRef);
        row.Value.Should().Be("hello");
        listed.Select(s => s.Id).Should().Equal(serviceId);
    }

    /// <summary>The host facts the factory builds follow the binding's permissions: without the services right, no services.</summary>
    [Fact]
    public async Task TheFacts_FollowTheBindingsPermissionsAsync()
    {
        var pluginId = $"host-rights-{Guid.NewGuid():N}";
        var (clientId, _) = await SeedClientWithServiceAsync();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>()
            .Create(PluginCallBinding.ForClient(
                pluginId, clientId, $"ref-{Guid.NewGuid():N}", ClientPermission.ViewDomains));

        (await services.Host.ListServicesAsync(CancellationToken.None)).Should().BeEmpty();
        (await services.Host.GetProfileAsync(CancellationToken.None)).Should().NotBeNull();
    }

    /// <summary>
    /// Each call gets its own quota signal: a store that refuses a write over the quota raises the signal of its own call
    /// and leaves another call's signal down.
    /// </summary>
    [Fact]
    public async Task TheQuotaSignal_IsPerCallAsync()
    {
        var pluginId = $"host-quota-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var oneKey = Options.Create(new PluginApiOptions { MaxStoreKeysPerScope = 1 });
        var callServices = new HostPluginCallServices(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IPluginHostFactsReader>(),
            sp.GetRequiredService<IIdentityProvider>(),
            sp.GetRequiredService<IPayerCurrencyResolver>(),
            oneKey,
            sp.GetRequiredService<PluginSshAccessService>());

        var over = callServices.Create(PluginCallBinding.WithoutClient(pluginId));
        var other = callServices.Create(PluginCallBinding.WithoutClient(pluginId));
        await over.Store.SetAsync("first", "1", CancellationToken.None);
        var second = () => over.Store.SetAsync("second", "2", CancellationToken.None);

        await second.Should().ThrowAsync<Exception>();
        over.Quota.Raised.Should().BeTrue();
        other.Quota.Raised.Should().BeFalse();
        other.Quota.Should().NotBeSameAs(over.Quota);
    }

    /// <summary>A binding without a client writes plugin wide (null reference) and sees no host facts.</summary>
    [Fact]
    public async Task BindingWithoutAClient_StoresPluginWide_AndSeesNoFactsAsync()
    {
        var pluginId = $"host-wide-{Guid.NewGuid():N}";
        await SeedClientWithServiceAsync();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(PluginCallBinding.WithoutClient(pluginId));
        await services.Store.SetAsync("greeting", "hello", CancellationToken.None);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.PluginDataEntries.AsNoTracking().SingleAsync(d => d.PluginId == pluginId);
        row.ClientRef.Should().BeNull();
        (await services.Host.GetProfileAsync(CancellationToken.None)).Should().BeNull();
        (await services.Host.ListServicesAsync(CancellationToken.None)).Should().BeEmpty();
        (await services.Host.ListInvoicesAsync(CancellationToken.None)).Should().BeEmpty();
    }

    /// <summary>
    /// An admin-area binding that carries a client (what #644 will pass) is the same record as the client's own, so a
    /// second instance built from an equal binding reads what the first wrote and sees the same facts.
    /// </summary>
    [Fact]
    public async Task EqualBindings_ShareTheStoreAndTheFactsAsync()
    {
        var pluginId = $"host-shared-{Guid.NewGuid():N}";
        var (clientId, _) = await SeedClientWithServiceAsync();

        using var scope = factory.Services.CreateScope();
        var factoryOfServices = scope.ServiceProvider.GetRequiredService<IPluginCallServices>();
        var clientRef = scope.ServiceProvider.GetRequiredService<IPluginReferences>().ForClient(pluginId, clientId);
        var own = factoryOfServices.Create(
            PluginCallBinding.ForClient(pluginId, clientId, clientRef, ClientPermission.All));
        var admin = factoryOfServices.Create(
            PluginCallBinding.ForClient(pluginId, clientId, clientRef, ClientPermission.All));
        await own.Store.SetAsync("note", "from-client-area", CancellationToken.None);

        (await admin.Store.GetAsync("note", CancellationToken.None)).Should().Be("from-client-area");
        (await admin.Host.GetProfileAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.GetProfileAsync(CancellationToken.None));
        (await admin.Host.ListServicesAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.ListServicesAsync(CancellationToken.None));
        (await admin.Host.ListInvoicesAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.ListInvoicesAsync(CancellationToken.None));
    }

    /// <summary>
    /// The implementations expose exactly their interface's methods; no store method and no host read takes an id or a
    /// scope, and the only id the host API names is the service of the two SSH members, which the host checks is the
    /// caller's.
    /// </summary>
    [Fact]
    public void Surface_IsExactlyTheInterfaces_WithNoIdOrScopeParameter()
    {
        PublicMethodNames(typeof(PluginHostApi)).Should().BeEquivalentTo(InterfaceMethodNames(typeof(IPluginHostApi)));
        PublicMethodNames(typeof(PluginStore)).Should().BeEquivalentTo(InterfaceMethodNames(typeof(IPluginStore)));

        string[] ssh = [nameof(IPluginHostApi.IssueSshAccessAsync), nameof(IPluginHostApi.RevokeSshAccessAsync)];
        var parameters = typeof(IPluginHostApi).GetMethods().Where(m => !ssh.Contains(m.Name))
            .Concat(typeof(IPluginStore).GetMethods()).SelectMany(m => m.GetParameters());
        parameters.Should().NotContain(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(int?));
        parameters.Select(p => p.Name!.ToLowerInvariant())
            .Should().NotContain(n => n.Contains("clientid") || n.Contains("pluginid") || n.Contains("scope"));
    }

    /// <summary>Names of the public instance methods a type declares itself.</summary>
    /// <param name="type">The implementation.</param>
    /// <returns>The method names.</returns>
    private static string[] PublicMethodNames(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToArray();

    /// <summary>Names of the methods an interface declares.</summary>
    /// <param name="type">The interface.</param>
    /// <returns>The method names.</returns>
    private static string[] InterfaceMethodNames(Type type) => type.GetMethods().Select(m => m.Name).ToArray();

    /// <summary>Seeds a client that owns one service.</summary>
    /// <returns>The client id and the service id.</returns>
    private async Task<(int ClientId, int ServiceId)> SeedClientWithServiceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create($"call-services-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", Origin);
        var group = ProductGroup.Create($"call-services-{Guid.NewGuid():N}", null);
        context.Clients.Add(client);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m, Origin);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        var service = ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, Origin);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        return (client.Id, service.Id);
    }

    /// <summary>
    /// The host API hands both SSH members to the SSH access service (#758): an admin-area call, which acts for no
    /// client, is refused with <c>NotAllowed</c> and touches nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SshAccess_OnAnAdminCall_IsRefusedAsNotAllowed()
    {
        using var scope = factory.Services.CreateScope();
        var host = scope.ServiceProvider.GetRequiredService<IPluginCallServices>()
            .Create(PluginCallBinding.WithoutClient("p")).Host;

        var issue = () => host.IssueSshAccessAsync(1, "label", CancellationToken.None);
        var revoke = () => host.RevokeSshAccessAsync(1, Guid.NewGuid(), CancellationToken.None);

        (await issue.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        (await revoke.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }
}
