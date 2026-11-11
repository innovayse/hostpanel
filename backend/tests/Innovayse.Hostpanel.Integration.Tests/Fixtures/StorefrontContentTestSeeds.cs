using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the storefront-content modules' per-use-case Integration tests (Testimonials,
/// Partners, Locations, Projects, Faqs): an admin bearer token, the request bodies every test starts from,
/// reading a refusal's error code, and a language-stamped anonymous request.
/// </summary>
internal static class StorefrontContentTestSeeds
{
    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Asserts the refusal carries the expected error code, which is what a client branches on.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>Creates a record through the admin API and returns the id the server assigned.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="route">The collection route, e.g. <c>/api/admin/testimonials</c>.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The new record's id.</returns>
    public static async Task<int> CreateAsync(HttpClient client, string route, object body)
    {
        var response = await client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>GETs a public feed as an anonymous caller, in the given language.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="route">The public route.</param>
    /// <param name="language">The <c>X-Language</c> value, or <see langword="null"/> to send none.</param>
    /// <returns>The raw response.</returns>
    public static async Task<HttpResponseMessage> GetPublicAsync(
        IntegrationTestFactory factory, string route, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (language is not null)
        {
            request.Headers.Add("X-Language", language);
        }

        return await factory.CreateClient().SendAsync(request);
    }

    /// <summary>The body for creating a testimonial with an English and an Armenian translation.</summary>
    /// <param name="sortOrder">Display order.</param>
    /// <param name="isActive">Whether the testimonial is created active.</param>
    /// <param name="publishedAt">The admin-set date, or <c>null</c> to leave it unset.</param>
    /// <returns>The request body.</returns>
    public static object TestimonialBody(int sortOrder = 0, bool isActive = true, DateTimeOffset? publishedAt = null) => new
    {
        name = "Ann Client",
        company = (string?)"Acme",
        avatarUrl = (string?)null,
        rating = 5,
        sortOrder,
        isActive,
        publishedAt,
        translations = new
        {
            en = new { role = "CEO", quote = "English quote" },
            hy = new { role = "Գործադիր", quote = "Armenian quote" },
        },
    };

    /// <summary>The body for creating a partner with an English and an Armenian translation.</summary>
    /// <param name="sortOrder">Display order.</param>
    /// <param name="isActive">Whether the partner is created active.</param>
    /// <returns>The request body.</returns>
    public static object PartnerBody(int sortOrder = 0, bool isActive = true) => new
    {
        logoUrl = (string?)"/uploads/branding/content/seed/logo.png",
        url = (string?)"https://partner.example",
        sortOrder,
        isActive,
        translations = new
        {
            en = new { name = "English name" },
            hy = new { name = "Armenian name" },
        },
    };

    /// <summary>The body for creating a server location with an English and an Armenian translation.</summary>
    /// <param name="sortOrder">Display order.</param>
    /// <param name="isActive">Whether the location is created active.</param>
    /// <returns>The request body.</returns>
    public static object LocationBody(int sortOrder = 0, bool isActive = true) => new
    {
        countryCode = "de",
        sortOrder,
        isActive,
        translations = new
        {
            en = new { name = "English name", city = (string?)"English city", latencyNote = (string?)"English note" },
            hy = new { name = "Armenian name", city = (string?)null, latencyNote = (string?)null },
        },
    };

    /// <summary>The body for creating a project with an English and an Armenian translation.</summary>
    /// <param name="sortOrder">Display order.</param>
    /// <param name="isActive">Whether the project is created active.</param>
    /// <param name="slug">The project slug; unique per call when omitted.</param>
    /// <returns>The request body.</returns>
    public static object ProjectBody(int sortOrder = 0, bool isActive = true, string? slug = null) => new
    {
        slug = slug ?? $"project-{Guid.NewGuid():n}",
        url = (string?)"https://project.example",
        imageUrls = new[] { "/uploads/branding/content/seed/a.png" },
        sortOrder,
        isActive,
        translations = new
        {
            en = new { title = "English title", summary = "English summary", description = (string?)null },
            hy = new { title = "Armenian title", summary = (string?)null, description = (string?)null },
        },
    };

    /// <summary>The body for creating a question with an English and an Armenian translation.</summary>
    /// <param name="sortOrder">Display order.</param>
    /// <param name="isActive">Whether the question is created active.</param>
    /// <returns>The request body.</returns>
    public static object FaqBody(int sortOrder = 0, bool isActive = true) => new
    {
        category = "general",
        sortOrder,
        isActive,
        translations = new
        {
            en = new { question = "English question?", answer = "English answer." },
            hy = new { question = "Armenian question?", answer = "Armenian answer." },
        },
    };
}
