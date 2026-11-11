using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListEmailTemplates</c> use case: <c>GET /api/admin/email-templates</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEmailTemplatesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The migration seeds seven templates, "welcome" among them; the admin list shows them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_ShowsTheSeededTemplates()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/email-templates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        templates!.Select(t => t.GetProperty("slug").GetString()).Should().Contain("welcome");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-templates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
