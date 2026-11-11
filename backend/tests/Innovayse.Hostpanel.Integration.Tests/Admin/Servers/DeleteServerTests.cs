using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>DELETE /api/admin/servers/{id}</c>: creating then deleting a server removes it from the
/// list, deleting it again answers <c>SERVER_NOT_FOUND</c>, a server a client service still lives
/// on is refused with <c>SERVER_IN_USE</c>, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteServerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creating then deleting a server removes it from the list; deleting again answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenDelete_RemovesTheServerAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var delete = await client.DeleteAsync($"/api/admin/servers/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/servers");
        list!.Should().NotContain(r => r.GetProperty("name").GetString() == name);

        var deleteAgain = await client.DeleteAsync($"/api/admin/servers/{id}");
        deleteAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(deleteAgain, HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>A server a client service still lives on is refused with 409 and stays in the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServerHostingAService_IsRefusedAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        await PlaceServiceOnAsync(id);

        var delete = await client.DeleteAsync($"/api/admin/servers/{id}");

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ServersTestSeeds.AssertCodeAsync(delete, HostpanelErrorCodes.SERVER_IN_USE);
        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/servers");
        list!.Should().Contain(r => r.GetProperty("name").GetString() == name);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "delete-server-forbidden");

        var response = await client.DeleteAsync($"/api/admin/servers/{ServersTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync($"/api/admin/servers/{ServersTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Activates a fresh client service on the server, so the server is in use.</summary>
    /// <param name="serverId">The server the service is placed on.</param>
    /// <returns>A task that completes once the service is saved.</returns>
    private async Task PlaceServiceOnAsync(int serverId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var owner = Client.Create($"server-use-{Guid.NewGuid():N}", "Server", "User", $"server-use-{Guid.NewGuid():N}@example.test", SeededAt);
        var group = ProductGroup.Create($"server-use-{Guid.NewGuid():N}", null);
        context.Clients.Add(owner);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m, SeededAt);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(owner.Id, product.Id, BillingCycle.Monthly, SeededAt);
        await ServicesTestSeeds.AddActivatedAsync(context, service, $"ref-{Guid.NewGuid():N}", SeededAt, serverId);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
