using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>GetMyDomainNameservers</c> use case:
/// <c>GET /api/me/domains/{id}/nameservers</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyDomainNameserversTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Reading nameservers refuses a stranger and, on the owner's own domain, returns the two
    /// just set through <c>UpdateMyDomainNameservers</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetNameservers_RefusesAStrangerAndServesTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-nameservers");
        await owner.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/nameservers",
            new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } });

        var strangersAttempt = await stranger.GetAsync($"/api/me/domains/{domainId}/nameservers");
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.GetAsync($"/api/me/domains/{domainId}/nameservers");
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ownersAttempt.Content.ReadAsStringAsync();
        json.Should().Contain("ns1.example.com");
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetNameservers_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}/nameservers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetNameservers_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "getmydomainnameservers-validation");

        var response = await client.GetAsync("/api/me/domains/0/nameservers");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
