using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>CancelStatusMaintenance</c> use case: <c>POST /api/admin/status/maintenance/{id}/cancel</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A cancelled window leaves the public list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_RemovesTheWindowFromThePublicList()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);

        var response = await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Should().NotContain(m => m.GetProperty("id").GetInt32() == id);
    }

    /// <summary>Cancelling twice answers 409 with the already-closed code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_Twice_RefusesWithConflict()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);
        await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}/cancel", null);

        var response = await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_MAINTENANCE_ALREADY_CLOSED);
    }

    /// <summary>An id that names no window answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/2147483000/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_MAINTENANCE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Cancel_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/1/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
