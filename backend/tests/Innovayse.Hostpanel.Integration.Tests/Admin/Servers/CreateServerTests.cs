using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>POST /api/admin/servers</c>: a created server appears in the list without its stored
/// secret, an empty name is refused <c>VALIDATION_ERROR</c> by the validator before the handler,
/// a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential. This use case always succeeds once validation has passed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateServerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A created server appears in the list, and its stored secret never appears in the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenList_ShowsTheServerWithoutItsSecretAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";

        await ServersTestSeeds.CreateServerAsync(client, name);

        var response = await client.GetAsync("/api/admin/servers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("top-secret-access-hash");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var row = doc.RootElement.EnumerateArray().Single(r => r.GetProperty("name").GetString() == name);
        row.GetProperty("credentials").TryGetProperty("accessHash", out _).Should().BeFalse("the DTO never carries a server's credentials");
    }

    /// <summary>A server with an empty name is refused by the validator before it reaches the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnEmptyNameAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/servers", ServersTestSeeds.ServerBody(string.Empty, hostname: "empty-name.example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A body that leaves out one of the nested groups (endpoint, credentials, limits, hosting, name servers) is refused at binding.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesABodyWithoutItsNestedGroupsAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/servers", new
        {
            name = "Missing-groups",
            module = ServerModule.Cwp7,
            username = "root",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "create-server-forbidden");

        var response = await client.PostAsJsonAsync("/api/admin/servers", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/servers", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
