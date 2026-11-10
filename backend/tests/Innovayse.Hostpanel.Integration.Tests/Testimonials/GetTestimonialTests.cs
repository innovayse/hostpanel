using System.Net;
using System.Net.Http.Headers;
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
    /// <summary>The moment the host's clock stands at while the testimonial is created.</summary>
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A stored testimonial is returned with its id and every translation.</summary>
    [Fact]
    public async Task Get_ReturnsTheRecordWithTranslations()
    {
        using var host = new ClockedHost(factory, CreatedAt);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody(sortOrder: 4));

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/testimonials/{id}");

        fetched.GetProperty("id").GetInt32().Should().Be(id);
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(4);
        fetched.GetProperty("createdAt").GetDateTimeOffset().Should().Be(CreatedAt);
        fetched.GetProperty("translations").GetProperty("en").GetProperty("quote").GetString().Should().Be("English quote");
    }

    /// <summary>An id that names no testimonial answers 404 with the not-found code.</summary>
    [Fact]
    public async Task Get_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/testimonials/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TESTIMONIAL_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused as a validation failure before the handler runs.</summary>
    [Fact]
    public async Task Get_NonPositiveId_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/testimonials/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/testimonials/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
