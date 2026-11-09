using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>UpdateSlideOrder</c> use case: <c>PUT /api/admin/slides/order</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateSlideOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reordering two slides is reflected in the admin list, ordered by the new sort order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOrder_ReordersSlides()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var firstCreated = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(sortOrder: 0));
        var firstId = await firstCreated.Content.ReadFromJsonAsync<int>();
        var secondCreated = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(sortOrder: 1));
        var secondId = await secondCreated.Content.ReadFromJsonAsync<int>();

        var reordered = await client.PutAsJsonAsync("/api/admin/slides/order", new
        {
            items = new[]
            {
                new { id = firstId, sortOrder = 9 },
                new { id = secondId, sortOrder = 1 },
            },
        });
        reordered.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/admin/slides");
        var indices = list!.Select((s, i) => (Id: s.GetProperty("id").GetInt32(), Index: i)).ToList();
        var secondIndex = indices.Single(x => x.Id == secondId).Index;
        var firstIndex = indices.Single(x => x.Id == firstId).Index;
        secondIndex.Should().BeLessThan(firstIndex);
    }

    /// <summary>An id that names no slide refuses the whole request with 404 and moves nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOrder_UnknownId_RefusesNotFoundAndMovesNothing()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(sortOrder: 70));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await client.PutAsJsonAsync("/api/admin/slides/order", new
        {
            items = new[] { new { id = 2147483000, sortOrder = 1 }, new { id, sortOrder = 5 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SLIDE_NOT_FOUND);
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(70);
    }

    /// <summary>A request naming more rows than the bound is refused as a validation failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOrder_TooManyItems_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var items = Enumerable.Range(1, SortOrderLimits.MaxItems + 1).Select(i => new { id = i, sortOrder = i }).ToArray();

        var response = await client.PutAsJsonAsync("/api/admin/slides/order", new { items });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An anonymous caller is refused with 401 before the request reaches the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOrder_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/admin/slides/order", new { items = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An item with a non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOrder_NonPositiveItemId_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/slides/order", new
        {
            items = new[] { new { id = 0, sortOrder = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
