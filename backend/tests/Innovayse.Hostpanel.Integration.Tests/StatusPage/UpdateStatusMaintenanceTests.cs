using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>UpdateStatusMaintenance</c> use case: <c>PUT /api/admin/status/maintenance/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The change shows in the public list: the new title, schedule and component set replace the old ones.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesTheAnnouncement()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, first) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var second = await StorefrontContentTestSeeds.CreateAsync(client, $"{StatusPageTestSeeds.AdminRoute}/components", new { groupId, name = "Mail", description = (string?)null, displayOrder = 1 });
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [first]);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}", StatusPageTestSeeds.MaintenanceBody([second], title: "Moved"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var window = (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Single(m => m.GetProperty("id").GetInt32() == id);
        window.GetProperty("title").GetString().Should().Be("Moved");
        window.GetProperty("componentIds").EnumerateArray().Select(c => c.GetInt32()).Should().Equal(second);
    }

    /// <summary>An id that names no window answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/2147483000", StatusPageTestSeeds.MaintenanceBody([componentId]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_MAINTENANCE_NOT_FOUND);
    }

    /// <summary>A cancelled window takes no change: 409 with the already-closed code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_CancelledWindow_RefusesWithConflict()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);
        (await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}/cancel", null)).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}", StatusPageTestSeeds.MaintenanceBody([componentId]));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_MAINTENANCE_ALREADY_CLOSED);
    }

    /// <summary>A component id that names no component is refused with 404 and the component not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownComponent_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}", StatusPageTestSeeds.MaintenanceBody([2147483000]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/1", StatusPageTestSeeds.MaintenanceBody([1]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
