using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="ServiceServerExtensions"/>, the lookup of the server a service is hosted on that every
/// provisioning command, query and event handler starts from, dispatched through the real
/// <see cref="IMessageBus"/> against the real PostgreSQL host.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceServerExtensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a client, a product and a server, and returns the ids a service needs.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The client id, product id and server id.</returns>
    private static async Task<ClientProductServerIds> SeedAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);

        var group = ProductGroup.Create($"service-server-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        context.Products.Add(product);

        var server = Server.Create(new ServerDetails(
            Name: $"srv-{Guid.NewGuid():N}",
            Hostname: "srv.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.Cwp7,
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

        return new(client.Id, product.Id, server.Id);
    }

    /// <summary>A service activated on a server resolves to that server.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FindServerAsync_ForAnAssignedService_ReturnsTheServerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId, serverId) = await SeedAsync(context);
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "ref-1", DateTimeOffset.UtcNow, serverId);
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var server = await bus.FindServerAsync(service, CancellationToken.None);

        server.Should().NotBeNull();
        server!.Id.Should().Be(serverId);
        server.Module.Should().Be(ServerModule.Cwp7);
        server.AccessHash.Should().Be("hash");
    }

    /// <summary>A service that was never assigned a server resolves to null.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FindServerAsync_ForAServiceWithNoServer_ReturnsNullAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId, _) = await SeedAsync(context);
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var server = await bus.FindServerAsync(service, CancellationToken.None);

        server.Should().BeNull();
    }
}
