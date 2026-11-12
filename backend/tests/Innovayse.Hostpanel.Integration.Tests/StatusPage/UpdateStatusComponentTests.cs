using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>UpdateStatusComponent</c> use case: <c>PUT /api/admin/status/components/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateStatusComponentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The new name and description show in the public overview.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ShowsTheNewNameInTheOverview()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}", new { name = "VPS", description = (string?)"Virtual servers" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var component = StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId).GetProperty("components").EnumerateArray().Single();
        component.GetProperty("name").GetString().Should().Be("VPS");
        component.GetProperty("description").GetString().Should().Be("Virtual servers");
    }

    /// <summary>An empty name is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_EmptyName_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}", new { name = " ", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An id that names no component answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/2147483000", new { name = "VPS", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/1", new { name = "VPS", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
