using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ReorderStatusComponents</c> use case: <c>PUT /api/admin/status/components/order</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReorderStatusComponentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The overview lists the components of the group in the order of the ids sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_ListsTheComponentsInTheNewOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, first) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var second = await StorefrontContentTestSeeds.CreateAsync(client, $"{StatusPageTestSeeds.AdminRoute}/components", new { groupId, name = "Mail", description = (string?)null, displayOrder = 1 });

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/order", new { groupId, componentIds = new[] { second, first } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId).GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("id").GetInt32()).Should().Equal(second, first);
    }

    /// <summary>A list that repeats an id or names a component of another group is refused with the order-mismatch code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_NotAnExactPermutation_RefusesWithMismatch()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, first) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var (_, foreign) = await StatusPageTestSeeds.CreateComponentAsync(client);

        var repeated = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/order", new { groupId, componentIds = new[] { first, first } });
        var other = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/order", new { groupId, componentIds = new[] { foreign } });

        repeated.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        other.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(other, HostpanelErrorCodes.STATUS_ORDER_MISMATCH);
    }

    /// <summary>A missing group id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_NoGroup_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/order", new { componentIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/order", new { groupId = 1, componentIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
