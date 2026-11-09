using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ToggleEmailTemplate</c> use case:
/// <c>PUT /api/admin/email-templates/{id}/toggle</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ToggleEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Toggling a template off is reflected when it is read back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Toggle_SwitchesActiveFlag()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync(
            "/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody($"test-{Guid.NewGuid():N}"));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var toggled = await client.PutAsJsonAsync($"/api/admin/email-templates/{id}/toggle", new { active = false });
        toggled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/email-templates/{id}");
        fetched.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    /// <summary>An id that names no template answers 404 with <c>EMAIL_TEMPLATE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Toggle_UnknownId_RefusesNotFound()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/email-templates/999999/toggle", new { active = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Toggle_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/admin/email-templates/1/toggle", new { active = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
