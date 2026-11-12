using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>DeleteStatusMaintenance</c> use case: <c>DELETE /api/admin/status/maintenance/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deleted window leaves the public list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheWindow()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Should().NotContain(m => m.GetProperty("id").GetInt32() == id);
    }

    /// <summary>An id that names no window answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_MAINTENANCE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
