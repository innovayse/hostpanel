using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>CreateEmailTemplate</c> use case: <c>POST /api/admin/email-templates</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a template and reading it back round-trips the content, active by default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var slug = $"test-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/email-templates/{id}");
        fetched.GetProperty("slug").GetString().Should().Be(slug);
        fetched.GetProperty("subject").GetString().Should().Be("Test subject {{name}}");
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    /// <summary>A second language version of the same slug is its own template, carrying its language.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_SecondLanguageVersion_IsStoredBesideTheFirst()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var slug = $"test-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await client.PostAsJsonAsync(
            "/api/admin/email-templates",
            new { slug, language = "ru", subject = "Тема", body = "<p>Текст</p>", description = (string?)null });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();
        var fetched = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/email-templates/{id}");
        fetched.GetProperty("language").GetString().Should().Be("ru");
        fetched.GetProperty("subject").GetString().Should().Be("Тема");
    }

    /// <summary>A slug holds one version per language, so a second one in the same language is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_SameLanguageTwice_RefusesConflict()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var slug = $"test-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_TEMPLATE_ALREADY_EXISTS);
    }

    /// <summary>A language the platform does not serve is refused, since no recipient could ever be matched to it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLanguage_RefusesBadRequest()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/email-templates",
            new { slug = $"test-{Guid.NewGuid():N}", language = "xx", subject = "S", body = "B", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody($"test-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
