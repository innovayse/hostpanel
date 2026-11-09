using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>UpdateMyDomainDnsRecord</c> use case:
/// <c>PUT /api/me/domains/{id}/dns/{recordId}</c>. The admin equivalent is
/// <c>UpdateDnsRecord</c>; the sibling client-facing use cases in the same lifecycle are
/// <c>AddMyDomainDnsRecord</c> and <c>DeleteMyDomainDnsRecord</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateMyDomainDnsRecordTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a DNS record refuses a stranger with <c>DOMAIN_NOT_FOUND</c> and round-trips for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDnsRecord_RefusesAStrangerAndRoundTripsForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "update-my-domain-dns-record");
        await owner.PostAsJsonAsync(
            $"/api/me/domains/{domainId}/dns",
            new { type = "A", host = "www", value = "203.0.113.10", ttl = 3600, priority = (int?)null });

        var domainAfterAdd = await owner.GetAsync($"/api/me/domains/{domainId}");
        using var domainBody = JsonDocument.Parse(await domainAfterAdd.Content.ReadAsStringAsync());
        var recordId = domainBody.RootElement.GetProperty("management").GetProperty("dnsRecords")[0].GetProperty("id").GetInt32();

        var updateBody = new { value = "203.0.113.20", ttl = 1800, priority = (int?)null };

        var strangersUpdate = await stranger.PutAsJsonAsync($"/api/me/domains/{domainId}/dns/{recordId}", updateBody);
        strangersUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersUpdate)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersUpdate = await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/dns/{recordId}", updateBody);
        ownersUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDnsRecord_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/dns/1",
            new { value = "203.0.113.20", ttl = 1800, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDnsRecord_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "updatemydomaindnsrecord-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/dns/1",
            new { value = "203.0.113.20", ttl = 1800, priority = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
