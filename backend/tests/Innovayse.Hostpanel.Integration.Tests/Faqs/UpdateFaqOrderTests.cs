using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Faqs;

/// <summary>
/// Integration tests for the <c>UpdateFaqOrder</c> use case: <c>PUT /api/admin/faqs/order</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateFaqOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The listed ids take their new positions and the public feed follows the new order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_MovesTheListedRecords()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var first = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/faqs", StorefrontContentTestSeeds.FaqBody(sortOrder: 70));
        var second = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/faqs", StorefrontContentTestSeeds.FaqBody(sortOrder: 80));

        var response = await client.PutAsJsonAsync("/api/admin/faqs/order", new
        {
            items = new[] { new { id = first, sortOrder = 90 }, new { id = second, sortOrder = 75 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var feed = await (await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/faqs"))
            .Content.ReadFromJsonAsync<JsonElement[]>();
        var ids = feed!.Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.IndexOf(second).Should().BeLessThan(ids.IndexOf(first));
    }

    /// <summary>An id that names no record refuses the whole request and moves nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_UnknownId_RefusesNotFoundAndMovesNothing()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/faqs", StorefrontContentTestSeeds.FaqBody(sortOrder: 70));

        var response = await client.PutAsJsonAsync("/api/admin/faqs/order", new
        {
            items = new[] { new { id = 2147483000, sortOrder = 1 }, new { id, sortOrder = 5 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.FAQ_NOT_FOUND);
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/faqs/{id}");
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(70);
    }

    /// <summary>A negative sort order is refused as a validation failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_NegativeSortOrder_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/faqs/order", new
        {
            items = new[] { new { id = 1, sortOrder = -1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reorder_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/faqs/order", new
        {
            items = new[] { new { id = 1, sortOrder = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
