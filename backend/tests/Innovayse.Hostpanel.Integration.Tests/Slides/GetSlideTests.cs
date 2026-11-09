using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>GetSlide</c> use case: <c>GET /api/admin/slides/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSlideTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known id returns the slide with its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_KnownId_ReturnsTheSlide()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");

        fetched.GetProperty("id").GetInt32().Should().Be(id);
    }

    /// <summary>An id that names no slide answers 404 with <c>SLIDE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesNotFound()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/slides/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SLIDE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/slides/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/slides/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
