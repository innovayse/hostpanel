using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>ModifyMyDomainContact</c> use case:
/// <c>PUT /api/me/domains/{id}/whois</c>. The admin equivalent is <c>ModifyDomainContact</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ModifyMyDomainContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Modifying the registrant contact refuses a stranger and applies for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_RefusesAStrangerAndAppliesForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "modify-my-domain-contact");
        var body = new
        {
            contact = new
            {
                firstName = "Jane",
                lastName = "Doe",
                email = "jane@example.com",
                phone = "+10000000000",
                address1 = "1 Example St",
                city = "Example City",
                state = "EX",
                postalCode = "00000",
                country = "US"
            }
        };

        var strangersAttempt = await stranger.PutAsJsonAsync($"/api/me/domains/{domainId}/whois", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/whois", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/whois",
            new { contact = new { firstName = "Jane", lastName = "Doe", email = "jane@example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "modifymydomaincontact-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/whois",
            new { contact = new { firstName = "Jane", lastName = "Doe", email = "jane@example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
