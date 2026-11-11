using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>AddMyDomainEmailForwardingRule</c> use case:
/// <c>POST /api/me/domains/{id}/email-forwarding</c>. The admin equivalent is
/// <c>AddEmailForwardingRule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddMyDomainEmailForwardingRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Adding an email forwarding rule refuses a stranger before anything is written, and for
    /// the owner persists a rule that then shows up on a read of the domain.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_RefusesAStrangerAndPersistsForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "add-my-domain-email-fwd-rule");
        var body = new { source = "info", destination = "info@example.com" };

        var strangersAttempt = await stranger.PostAsJsonAsync($"/api/me/domains/{domainId}/email-forwarding", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PostAsJsonAsync($"/api/me/domains/{domainId}/email-forwarding", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.Created);

        var domain = await owner.GetAsync($"/api/me/domains/{domainId}");
        var json = await domain.Content.ReadAsStringAsync();
        json.Should().Contain("info@example.com");
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/email-forwarding",
            new { source = "info", destination = "info@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddEmailForwardingRule_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "addmydomainemailforwardingrule-validation");

        var response = await client.PostAsJsonAsync(
            "/api/me/domains/0/email-forwarding",
            new { source = "info", destination = "me@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
