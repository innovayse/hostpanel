using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>ListClientEmailLogs</c> use case, reachable both through the
/// admin per-client route (<c>GET /api/admin/email-logs/client/{clientId}</c>) and the client
/// portal's own list (<c>GET /api/me/emails</c>).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientEmailLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client id that names nobody answers an empty page, not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_UnknownClient_ReturnsAnEmptyPage()
    {
        var client = await NotificationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/email-logs/client/999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
        page.GetProperty("pageNumber").GetInt32().Should().Be(1);
        page.GetProperty("totalPages").GetInt32().Should().Be(0);
    }

    /// <summary>The admin per-client route finds a log seeded straight at the client's own address, and lists nobody else's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_FindsTheClientsOwnLog()
    {
        var (_, clientId, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Hello, client");

        var admin = await NotificationsTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync($"/api/admin/email-logs/client/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Registering mailed the client a welcome as well (delivered asynchronously, so it may or may
        // not be listed yet): the route's contract is that everything it lists is addressed to this
        // client, and that the seeded row is among it.
        var items = page.GetProperty("items").EnumerateArray().ToList();
        page.GetProperty("totalCount").GetInt32().Should().Be(items.Count);
        items.Should().OnlyContain(i => i.GetProperty("to").GetString() == email);
        items.Select(i => i.GetProperty("subject").GetString()).Should().Contain("Hello, client");
    }

    /// <summary>The admin per-client route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminListByClient_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/email-logs/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client reading their own mail sees only their own address, never somebody else's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_List_ReturnsOwnLogsOnly()
    {
        var (http, _, _, email) = await NotificationsTestSeeds.RegisterClientAsync(factory);
        await NotificationsTestSeeds.SeedLogAsync(factory, email, "Mine");
        await NotificationsTestSeeds.SeedLogAsync(factory, $"someone-else-{Guid.NewGuid():N}@example.test", "Not mine");

        var response = await http.GetAsync("/api/me/emails?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var subjects = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("subject").GetString()).ToList();
        subjects.Should().Contain("Mine").And.NotContain("Not mine");
    }

    /// <summary>The client portal route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyEmails_List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/emails");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
