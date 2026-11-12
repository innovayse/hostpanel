using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>DeleteStatusComponent</c> use case: <c>DELETE /api/admin/status/components/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteStatusComponentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deleted component leaves the overview, and the maintenance window that listed it no longer lists it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheComponent_AndItsPlaceInAWindow()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var other = await StorefrontContentTestSeeds.CreateAsync(client, $"{StatusPageTestSeeds.AdminRoute}/components", new { groupId, name = "Mail", description = (string?)null, displayOrder = 1 });
        var windowId = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId, other]);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId).GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("id").GetInt32()).Should().Equal(other);
        var window = (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Single(m => m.GetProperty("id").GetInt32() == windowId);
        window.GetProperty("componentIds").EnumerateArray().Select(c => c.GetInt32()).Should().Equal(other);
    }

    /// <summary>An id that names no component answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/components/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/components/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
