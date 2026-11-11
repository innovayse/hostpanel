using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>CreateStatusComponent</c> use case: <c>POST /api/admin/status/components</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateStatusComponentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A new component is listed under its group, operational.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ListsTheComponentOperationalUnderItsGroup()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var (groupId, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var group = StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId);
        var component = group.GetProperty("components").EnumerateArray().Single();
        component.GetProperty("id").GetInt32().Should().Be(componentId);
        component.GetProperty("name").GetString().Should().Be("Web hosting");
        component.GetProperty("description").GetString().Should().Be("Shared hosting nodes");
        component.GetProperty("state").GetString().Should().Be("Operational");
    }

    /// <summary>A group id that names no group answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnknownGroup_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components",
            new { groupId = 2147483000, name = "Web hosting", description = (string?)null, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_GROUP_NOT_FOUND);
    }

    /// <summary>An empty name is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_EmptyName_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var groupId = await StatusPageTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components",
            new { groupId, name = "", description = (string?)null, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"{StatusPageTestSeeds.AdminRoute}/components",
            new { groupId = 1, name = "Web hosting", description = (string?)null, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
