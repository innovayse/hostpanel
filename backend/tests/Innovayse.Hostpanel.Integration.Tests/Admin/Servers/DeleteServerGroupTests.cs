using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>DELETE /api/admin/server-groups/{id}</c>: creating then deleting a group removes it,
/// deleting it again answers <c>SERVER_GROUP_NOT_FOUND</c>, a group a product still uses is refused
/// with <c>SERVER_GROUP_IN_USE</c>, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteServerGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating then deleting a group removes it; deleting again answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenDelete_RemovesTheGroupAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var groupName = $"Group-{Guid.NewGuid():N}";
        var groupId = await ServersTestSeeds.CreateServerGroupAsync(client, groupName);

        var delete = await client.DeleteAsync($"/api/admin/server-groups/{groupId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteAgain = await client.DeleteAsync($"/api/admin/server-groups/{groupId}");
        deleteAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(deleteAgain, HostpanelErrorCodes.SERVER_GROUP_NOT_FOUND);
    }

    /// <summary>A group a product still provisions into is refused with 409 and is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GroupUsedByAProduct_IsRefusedAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var groupId = await ServersTestSeeds.CreateServerGroupAsync(client, $"Group-{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var productGroup = ProductGroup.Create($"group-use-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(productGroup);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Products.Add(Product.Create(
                productGroup.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m,
                TestMoments.Seeded, serverGroupId: groupId));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var delete = await client.DeleteAsync($"/api/admin/server-groups/{groupId}");

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ServersTestSeeds.AssertCodeAsync(delete, HostpanelErrorCodes.SERVER_GROUP_IN_USE);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "delete-server-group-forbidden");

        var response = await client.DeleteAsync($"/api/admin/server-groups/{ServersTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/admin/server-groups/{ServersTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
