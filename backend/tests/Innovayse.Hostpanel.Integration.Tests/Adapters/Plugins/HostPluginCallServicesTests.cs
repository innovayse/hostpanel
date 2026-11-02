using System.Reflection;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Host;
using Innovayse.SDK.Plugins.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="HostPluginCallServices"/> against Postgres: the production container builds it, and one binding gives the
/// host facts and the store scope of the same client (or of none), with no id on any method a plugin can call.
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

    /// <summary>A binding with a client stores under the client's reference and lists that client's services.</summary>
    [Fact]
    public async Task BindingWithAClient_StoresUnderTheClientReference_AndListsItsServicesAsync()
    {
        var pluginId = $"host-bound-{Guid.NewGuid():N}";
        var (clientId, serviceId) = await SeedClientWithServiceAsync();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(new PluginCallBinding(pluginId, clientId));
        await services.Store.SetAsync("greeting", "hello", CancellationToken.None);
        var listed = await services.Host.ListServicesAsync(CancellationToken.None);

        var expectedRef = scope.ServiceProvider.GetRequiredService<IPluginReferences>().ForClient(pluginId, clientId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.PluginDataEntries.AsNoTracking().SingleAsync(d => d.PluginId == pluginId);
        row.ClientRef.Should().Be(expectedRef);
        row.Value.Should().Be("hello");
        listed.Select(s => s.Id).Should().Equal(serviceId);
    }

    /// <summary>A binding without a client writes plugin wide (null reference) and sees no host facts.</summary>
    [Fact]
    public async Task BindingWithoutAClient_StoresPluginWide_AndSeesNoFactsAsync()
    {
        var pluginId = $"host-wide-{Guid.NewGuid():N}";
        await SeedClientWithServiceAsync();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(new PluginCallBinding(pluginId, null));
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
        var own = factoryOfServices.Create(new PluginCallBinding(pluginId, clientId));
        var admin = factoryOfServices.Create(new PluginCallBinding(pluginId, clientId));
        await own.Store.SetAsync("note", "from-client-area", CancellationToken.None);

        (await admin.Store.GetAsync("note", CancellationToken.None)).Should().Be("from-client-area");
        (await admin.Host.GetProfileAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.GetProfileAsync(CancellationToken.None));
        (await admin.Host.ListServicesAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.ListServicesAsync(CancellationToken.None));
        (await admin.Host.ListInvoicesAsync(CancellationToken.None)).Should().BeEquivalentTo(await own.Host.ListInvoicesAsync(CancellationToken.None));
    }

    /// <summary>The implementations expose exactly their interface's methods, and no interface method takes an id or a scope.</summary>
    [Fact]
    public void Surface_IsExactlyTheInterfaces_WithNoIdOrScopeParameter()
    {
        PublicMethodNames(typeof(PluginHostApi)).Should().BeEquivalentTo(InterfaceMethodNames(typeof(IPluginHostApi)));
        PublicMethodNames(typeof(PluginStore)).Should().BeEquivalentTo(InterfaceMethodNames(typeof(IPluginStore)));

        var parameters = typeof(IPluginHostApi).GetMethods().Concat(typeof(IPluginStore).GetMethods()).SelectMany(m => m.GetParameters());
        parameters.Should().NotContain(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(int?));
        parameters.Select(p => p.Name!.ToLowerInvariant())
            .Should().NotContain(n => n.Contains("clientid") || n.Contains("pluginid") || n.Contains("scope"));
    }

    /// <summary>A null binding is refused.</summary>
    [Fact]
    public void Create_RefusesANullBinding()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>();

        var act = () => services.Create(null!);

        act.Should().Throw<ArgumentNullException>();
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
}
