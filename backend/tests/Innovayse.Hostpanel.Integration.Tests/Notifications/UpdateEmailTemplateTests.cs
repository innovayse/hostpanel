using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>UpdateEmailTemplate</c> use case: <c>PUT /api/admin/email-templates/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a template's content changes what is read back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RoundTrips()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var slug = $"test-{Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var updated = await client.PutAsJsonAsync(
            $"/api/admin/email-templates/{id}",
            new { subject = "Updated subject", body = "<p>Updated body</p>", description = "Updated" });
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/email-templates/{id}");
        fetched.GetProperty("subject").GetString().Should().Be("Updated subject");
    }

    /// <summary>An id that names no template answers 404 with <c>EMAIL_TEMPLATE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesNotFound()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/api/admin/email-templates/999999",
            new { subject = "New subject", body = "<p>New body</p>", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/admin/email-templates/1",
            new { subject = "New subject", body = "<p>New body</p>", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
