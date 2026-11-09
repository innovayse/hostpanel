using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Testimonials;

/// <summary>
/// Integration tests for the <c>GetTestimonial</c> use case: <c>GET /api/admin/testimonials/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTestimonialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A stored testimonial is returned with its id and every translation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ReturnsTheRecordWithTranslations()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody(sortOrder: 4));

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/testimonials/{id}");

        fetched.GetProperty("id").GetInt32().Should().Be(id);
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(4);
        fetched.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        fetched.GetProperty("translations").GetProperty("en").GetProperty("quote").GetString().Should().Be("English quote");
    }

    /// <summary>An id that names no testimonial answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/testimonials/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TESTIMONIAL_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused as a validation failure before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/testimonials/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/testimonials/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
