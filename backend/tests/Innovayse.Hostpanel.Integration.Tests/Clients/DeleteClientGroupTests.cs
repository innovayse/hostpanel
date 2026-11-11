using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>Integration tests for the <c>DeleteClientGroup</c> use case: <c>DELETE /api/admin/client-groups/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteClientGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting an empty group removes it: a later read of the id answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesAnEmptyGroup()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);

        var response = await admin.DeleteAsync($"{ClientGroupTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"{ClientGroupTestSeeds.Route}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A group that still has a client is refused with 409 and the group, and the client's link to it, stay.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_GroupWithAClient_RefusesAsInUse()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, id);

        var response = await admin.DeleteAsync($"{ClientGroupTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_IN_USE);
        (await ClientGroupTestSeeds.GetAsync(admin, id)).GetProperty("clientCount").GetInt32().Should().Be(1);
    }

    /// <summary>An id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"{ClientGroupTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_GROUP_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{ClientGroupTestSeeds.Route}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A client that joins a group after the delete use case checked for members is refused by the foreign key, and the
    /// context
    /// reports it as the persistence-agnostic conflict the delete handler answers as <c>CLIENT_GROUP_IN_USE</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SaveChanges_DeletingAGroupAClientJoinedMeanwhile_ReportsAConflict()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var id = await ClientGroupTestSeeds.CreateAsync(admin);
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = await context.ClientGroups.SingleAsync(g => g.Id == id);
        await ClientGroupTestSeeds.AssignAsync(factory, clientId, id);

        context.ClientGroups.Remove(group);
        var save = () => context.SaveChangesAsync(CancellationToken.None);

        await save.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}
