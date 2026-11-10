using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>AddMyDomainDnsRecord</c> use case:
/// <c>POST /api/me/domains/{id}/dns</c>. The admin equivalent is <c>AddDnsRecord</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddMyDomainDnsRecordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Adding a DNS record refuses a stranger before anything is written, and for the owner
    /// persists a record that then shows up on a read of the domain.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_RefusesAStrangerAndPersistsForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "add-my-domain-dns-record");
        var body = new { type = "A", host = "www", value = "203.0.113.10", ttl = 3600, priority = (int?)null };

        var strangersAttempt = await stranger.PostAsJsonAsync($"/api/me/domains/{domainId}/dns", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PostAsJsonAsync($"/api/me/domains/{domainId}/dns", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.Created);

        var domain = await owner.GetAsync($"/api/me/domains/{domainId}");
        var json = await domain.Content.ReadAsStringAsync();
        json.Should().Contain("203.0.113.10");
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/dns",
            new { type = "A", host = "www", value = "203.0.113.10", ttl = 3600, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddDnsRecord_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "addmydomaindnsrecord-validation");

        var response = await client.PostAsJsonAsync(
            "/api/me/domains/0/dns",
            new { type = "A", host = "www", value = "203.0.113.10", ttl = 3600, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
