using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>SetStatusComponentOverride</c> use case: <c>PUT /api/admin/status/components/{id}/override</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetStatusComponentOverrideTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The forced state shows in the public overview, and an outage makes the whole page an outage.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Set_ShowsTheStateAndRaisesTheOverallState()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/override", new { state = "Outage" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var overview = await StatusPageTestSeeds.GetOverviewAsync(factory);
        var component = StatusPageTestSeeds.GroupOf(overview, groupId).GetProperty("components").EnumerateArray().Single();
        component.GetProperty("state").GetString().Should().Be("Outage");
        overview.GetProperty("overallState").GetString().Should().Be("Outage");
    }

    /// <summary>A null state clears the override, so the component shows what its checks find: operational here.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Set_NullState_ClearsTheOverride()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/override", new { state = "Degraded" });

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/override", new { state = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var overview = await StatusPageTestSeeds.GetOverviewAsync(factory);
        var component = StatusPageTestSeeds.GroupOf(overview, groupId).GetProperty("components").EnumerateArray().Single();
        component.GetProperty("state").GetString().Should().Be("Operational");
    }

    /// <summary>An id that names no component answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Set_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/2147483000/override", new { state = "Degraded" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_COMPONENT_NOT_FOUND);
    }

    /// <summary>A number that names no state is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Set_UnknownState_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{componentId}/override", new { state = 99 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Set_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/1/override", new { state = "Degraded" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
