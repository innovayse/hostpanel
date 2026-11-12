using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>UpdateStatusComponentGroup</c> use case: <c>PUT /api/admin/status/groups/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateStatusComponentGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The new name shows in the public overview.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ShowsTheNewNameInTheOverview()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var groupId = await StatusPageTestSeeds.CreateGroupAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/{groupId}", new { name = "Core network" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId).GetProperty("name").GetString().Should().Be("Core network");
    }

    /// <summary>An empty name is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_EmptyName_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var groupId = await StatusPageTestSeeds.CreateGroupAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/{groupId}", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/2147483000", new { name = "Core network" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/1", new { name = "Core network" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
