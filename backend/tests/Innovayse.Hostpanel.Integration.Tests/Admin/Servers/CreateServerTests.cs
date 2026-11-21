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

        var row = (await ServersTestSeeds.SearchServersAsync(client, name)).Single(r => r.GetProperty("name").GetString() == name);
        row.GetRawText().Should().NotContain("top-secret-access-hash");
        row.GetProperty("credentials").TryGetProperty("accessHash", out _).Should().BeFalse("the DTO never carries a server's credentials");
    }

    /// <summary>A server is created trusting no self signed certificate unless the body says so, and the choice is stored and listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelfSignedCertificate_IsOffByDefaultAndStoredWhenAskedAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var plain = $"Server-{Guid.NewGuid():N}";
        var trusting = $"Server-{Guid.NewGuid():N}";

        (await client.PostAsJsonAsync("/api/admin/servers", ServersTestSeeds.ServerBody(plain))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(
            "/api/admin/servers", ServersTestSeeds.ServerBody(trusting, acceptsSelfSignedCertificate: true))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ServersTestSeeds.SearchServersAsync(client, plain)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();
        (await ServersTestSeeds.SearchServersAsync(client, trusting)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeTrue();
    }

    /// <summary>A body that does not mention the choice (an older script) creates a server that trusts nothing extra.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelfSignedCertificate_OmittedFromTheBody_IsOffAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var body = new
        {
            name,
            endpoint = new { hostname = $"{name}.example.com", ipAddress = (string?)null, assignedIpAddresses = (string?)null, useSSL = true },
            module = ServerModule.Cwp7,
            username = "root",
            credentials = new { },
            limits = new { isDefault = false, isDisabled = false },
            hosting = new { monthlyCost = 1m },
            nameServers = new { },
        };

        (await client.PostAsJsonAsync("/api/admin/servers", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ServersTestSeeds.SearchServersAsync(client, name)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();
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
