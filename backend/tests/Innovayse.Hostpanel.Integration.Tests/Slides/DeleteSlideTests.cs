using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>DeleteSlide</c> use case: <c>DELETE /api/admin/slides/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteSlideTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deleted slide can no longer be read back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheSlide()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var deleted = await client.DeleteAsync($"/api/admin/slides/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/admin/slides/{id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An id that names no slide answers 404 with <c>SLIDE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesNotFound()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/slides/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SLIDE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/admin/slides/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_NonPositiveId_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/slides/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
