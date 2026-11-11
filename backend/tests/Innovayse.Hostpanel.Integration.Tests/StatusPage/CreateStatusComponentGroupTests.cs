using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>CreateStatusComponentGroup</c> use case: <c>POST /api/admin/status/groups</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateStatusComponentGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A created group shows up in the public overview, with no components yet.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ShowsTheGroupInThePublicOverview()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups", new { name = "Network", displayOrder = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var id = await response.Content.ReadFromJsonAsync<int>();
        var group = StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), id);
        group.GetProperty("name").GetString().Should().Be("Network");
        group.GetProperty("components").GetArrayLength().Should().Be(0);
    }

    /// <summary>An empty name is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_EmptyName_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups", new { name = "", displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups", new { name = "Network", displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
