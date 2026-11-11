using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>ListDomains</c> use case: <c>GET /api/domains</c>, the admin
/// paginated listing of every registered domain.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An unauthenticated caller is refused with 401 before any query runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A signed-in Client, who does not hold the Admin role this
    /// admin-only listing requires, is refused with 403.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminOnly_ClientForbiddenAsync()
    {
        var (client, _, _) = await DomainsTestSeeds.RegisterClientAsync(factory, "list-domains-forbidden");

        var response = await client.GetAsync("/api/domains");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>An admin lists domains and sees the one just registered for a client, named after that client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_AdminSeesARegisteredDomainAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, ownerClientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "list-domains-owner");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId);

        var response = await admin.GetAsync("/api/domains?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(d => d.GetProperty("id").GetInt32() == domainId)
            .GetProperty("clientName").GetString().Should().Be("Domain Tester");
    }

    /// <summary>A page number below one is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListDomains_PageBelowOne_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains?page=0&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
