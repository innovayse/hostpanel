using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>SetMyDomainDnsManagement</c> use case:
/// <c>PUT /api/me/domains/{id}/dns-management</c>. The admin equivalent is
/// <c>SetDnsManagement</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetMyDomainDnsManagementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>DNS management toggling refuses a stranger's attempt and applies for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDnsManagement_RefusesAStrangerAndAppliesForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "set-my-domain-dns-mgmt");
        var body = new Dictionary<string, bool> { ["enabled"] = true };

        var strangersAttempt = await stranger.PutAsJsonAsync($"/api/me/domains/{domainId}/dns-management", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/dns-management", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDnsManagement_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/dns-management", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetDnsManagement_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "setmydomaindnsmanagement-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/dns-management",
            new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
