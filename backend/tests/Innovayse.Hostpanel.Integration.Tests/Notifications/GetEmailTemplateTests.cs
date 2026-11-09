using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>GetEmailTemplate</c> use case: <c>GET /api/admin/email-templates/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known id returns the template with its content.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_KnownId_ReturnsTheTemplate()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);
        var slug = $"test-{Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync("/api/admin/email-templates", NotificationsTestSeeds.CreateTemplateBody(slug));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/email-templates/{id}");

        fetched.GetProperty("slug").GetString().Should().Be(slug);
    }

    /// <summary>An id that names no template answers 404 with <c>EMAIL_TEMPLATE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesNotFound()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/email-templates/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await NotificationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-templates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
