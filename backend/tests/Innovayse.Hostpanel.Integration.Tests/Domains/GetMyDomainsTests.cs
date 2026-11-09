using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>GetMyDomains</c> use case: <c>GET /api/me/domains</c>, the
/// client-facing listing of the authenticated caller's own domains.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary><c>GET /api/me/domains</c> without auth returns 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyDomains_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Registers a user, waits for the Client record to be created by Wolverine, then calls
    /// <c>GET /api/me/domains</c> and expects 200 OK even with no domains yet.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyDomains_ClientCanSeeOwnDomainsAsync()
    {
        var (owner, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "get-my-domains-empty");

        var response = await owner.GetAsync("/api/me/domains");
        var debugBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET /api/me/domains returned: {debugBody}");
    }

    /// <summary>The caller's own domain shows up in their list; nothing else could, since only one exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyDomains_ReturnsOwnDomainsAsync()
    {
        var (_, owner, domainId, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domains");

        var response = await owner.GetAsync("/api/me/domains");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain($"\"id\":{domainId}");
    }
}
