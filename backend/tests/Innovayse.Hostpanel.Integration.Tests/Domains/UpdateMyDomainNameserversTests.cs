using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>UpdateMyDomainNameservers</c> use case:
/// <c>PUT /api/me/domains/{id}/nameservers</c>. The admin equivalent is
/// <c>UpdateNameservers</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateMyDomainNameserversTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Replacing nameservers refuses a stranger and applies for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_RefusesAStrangerAndAppliesForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "update-my-domain-nameservers");
        var body = new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } };

        var strangersAttempt = await stranger.PutAsJsonAsync($"/api/me/domains/{domainId}/nameservers", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/nameservers", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Fewer than two nameservers refuses with 400 for the owner too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithOneEntry_Returns400Async()
    {
        var (_, owner, domainId, _) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "update-my-domain-nameservers-invalid");

        var response = await owner.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/nameservers", new { nameservers = new[] { "ns1.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/nameservers",
            new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "updatemydomainnameservers-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/nameservers",
            new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
