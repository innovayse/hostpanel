using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>UpdateSlide</c> use case: <c>PUT /api/admin/slides/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateSlideTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A slide updated with a replaced translation set reflects the new fields and translation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesFieldsAndTranslations()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var updateBody = new
        {
            iconName = "server",
            brandColor = "#22c55e",
            imageUrl = "/uploads/slides/updated.png",
            demoUrl = (string?)null,
            learnMoreUrl = (string?)null,
            productId = (int?)null,
            sortOrder = 3,
            isActive = false,
            targetAudience = SlideAudience.Guest,
            visibleFrom = (DateTimeOffset?)null,
            visibleUntil = (DateTimeOffset?)null,
            translations = new[]
            {
                new
                {
                    locale = "ru",
                    title = "Хостинг",
                    tagline = (string?)null,
                    description = (string?)null,
                    features = (string[]?)null,
                    ctaText = (string?)null,
                    ctaUrl = (string?)null,
                },
            },
        };
        var updated = await client.PutAsJsonAsync($"/api/admin/slides/{id}", updateBody);
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");
        afterUpdate.GetProperty("iconName").GetString().Should().Be("server");
        afterUpdate.GetProperty("isActive").GetBoolean().Should().BeFalse();
        var translations = afterUpdate.GetProperty("translations").EnumerateArray().ToList();
        translations.Should().ContainSingle();
        translations[0].GetProperty("locale").GetString().Should().Be("ru");
    }

    /// <summary>An id that names no slide answers 404 with <c>SLIDE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesNotFound()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/slides/999999", SlidesTestSeeds.CreateBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SLIDE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/slides/1", SlidesTestSeeds.CreateBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A negative sort order is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_NegativeSortOrder_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/slides/999999", SlidesTestSeeds.CreateBody(sortOrder: -1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>Pointing a slide at a product that does not exist answers 404 with <c>PRODUCT_NOT_FOUND</c> and keeps the slide as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownProduct_RefusesNotFound()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/slides/{id}", SlidesTestSeeds.CreateBody(sortOrder: 5, productId: SlidesTestSeeds.NoSuchId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        var afterRefusal = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");
        afterRefusal.GetProperty("sortOrder").GetInt32().Should().Be(0);
    }
}
