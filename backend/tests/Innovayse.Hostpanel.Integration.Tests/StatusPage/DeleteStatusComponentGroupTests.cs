using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>DeleteStatusComponentGroup</c> use case: <c>DELETE /api/admin/status/groups/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteStatusComponentGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An empty group is removed and leaves the overview.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesAnEmptyGroup()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var groupId = await StatusPageTestSeeds.CreateGroupAsync(client);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/groups/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusPageTestSeeds.GetOverviewAsync(factory)).GetProperty("groups").EnumerateArray()
            .Should().NotContain(g => g.GetProperty("id").GetInt32() == groupId);
    }

    /// <summary>A group that still has components is refused with 409 and the not-empty code, and keeps them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_GroupWithComponents_RefusesWithConflict()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, _) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/groups/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_EMPTY);
        StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId).GetProperty("components").GetArrayLength().Should().Be(1);
    }

    /// <summary>An id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/groups/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/groups/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
