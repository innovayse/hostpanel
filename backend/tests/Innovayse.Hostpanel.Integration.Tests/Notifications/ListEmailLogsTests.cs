using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListEmailLogs</c> use case: <c>GET /api/admin/email-logs</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListEmailLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A log written straight to the database shows up in the admin flat list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_ShowsASeededLog()
    {
        await NotificationsTestSeeds.SeedLogAsync(factory, $"flat-list-{Guid.NewGuid():N}@example.test", "Flat list entry");
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/email-logs?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        logs!.Select(l => l.GetProperty("subject").GetString()).Should().Contain("Flat list entry");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
