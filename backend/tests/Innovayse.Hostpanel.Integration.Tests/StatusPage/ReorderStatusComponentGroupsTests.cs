using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ReorderStatusComponentGroups</c> use case: <c>PUT /api/admin/status/groups/order</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReorderStatusComponentGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The overview lists the groups in the order of the ids sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_ListsTheGroupsInTheNewOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        await StatusPageTestSeeds.CreateGroupAsync(client);
        await StatusPageTestSeeds.CreateGroupAsync(client);
        var current = (await StatusPageTestSeeds.GetOverviewAsync(factory)).GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("id").GetInt32()).ToList();
        var reversed = current.AsEnumerable().Reverse().ToList();

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/order", new { groupIds = reversed });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusPageTestSeeds.GetOverviewAsync(factory)).GetProperty("groups").EnumerateArray()
            .Select(g => g.GetProperty("id").GetInt32()).Should().Equal(reversed);
    }

    /// <summary>A list that leaves a group out is refused with the order-mismatch code and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_ListMissingAGroup_RefusesWithMismatch()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var first = await StatusPageTestSeeds.CreateGroupAsync(client);
        await StatusPageTestSeeds.CreateGroupAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/order", new { groupIds = new[] { first } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_ORDER_MISMATCH);
    }

    /// <summary>An empty list is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_EmptyList_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/order", new { groupIds = Array.Empty<int>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/groups/order", new { groupIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
