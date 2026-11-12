using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>CreateStatusMaintenance</c> use case: <c>POST /api/admin/status/maintenance</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An announced window shows in the public list with its schedule and affected components, without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ShowsTheWindowInThePublicList()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var id = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId]);

        var window = (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Single(m => m.GetProperty("id").GetInt32() == id);
        window.GetProperty("title").GetString().Should().Be("Network upgrade");
        window.GetProperty("state").GetString().Should().Be("Scheduled");
        window.GetProperty("componentIds").EnumerateArray().Select(c => c.GetInt32()).Should().Equal(componentId);
    }

    /// <summary>A component id that names no component is refused with 404 and the component not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnknownComponent_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(StatusPageTestSeeds.AdminMaintenanceRoute, StatusPageTestSeeds.MaintenanceBody([2147483000]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>A window that ends before it starts is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_EndBeforeStart_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var start = DateTimeOffset.UtcNow.AddHours(3);

        var response = await client.PostAsJsonAsync(StatusPageTestSeeds.AdminMaintenanceRoute, StatusPageTestSeeds.MaintenanceBody([componentId], start, start.AddHours(-1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A window with no affected component, or no title, is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_NoComponentsOrTitle_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var noComponents = await client.PostAsJsonAsync(StatusPageTestSeeds.AdminMaintenanceRoute, StatusPageTestSeeds.MaintenanceBody([]));
        var noTitle = await client.PostAsJsonAsync(StatusPageTestSeeds.AdminMaintenanceRoute, StatusPageTestSeeds.MaintenanceBody([componentId], title: " "));

        noComponents.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noTitle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(StatusPageTestSeeds.AdminMaintenanceRoute, StatusPageTestSeeds.MaintenanceBody([1]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
