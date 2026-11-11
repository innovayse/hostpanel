using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>UpdateMyDomainEmailForwardingRule</c> use case:
/// <c>PUT /api/me/domains/{id}/email-forwarding/{ruleId}</c>. The admin equivalent is
/// <c>UpdateEmailForwardingRule</c>; the sibling client-facing use cases in the same lifecycle
/// are <c>AddMyDomainEmailForwardingRule</c> and <c>DeleteMyDomainEmailForwardingRule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateMyDomainEmailForwardingRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a rule refuses a stranger with <c>DOMAIN_NOT_FOUND</c> and round-trips for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_RefusesAStrangerAndRoundTripsForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "update-my-domain-email-fwd-rule");
        await owner.PostAsJsonAsync(
            $"/api/me/domains/{domainId}/email-forwarding",
            new { source = "info", destination = "info@example.com" });

        var domainAfterAdd = await owner.GetAsync($"/api/me/domains/{domainId}");
        using var domainBody = JsonDocument.Parse(await domainAfterAdd.Content.ReadAsStringAsync());
        var ruleId = domainBody.RootElement.GetProperty("management").GetProperty("emailForwardingRules")[0].GetProperty("id").GetInt32();

        var updateBody = new { source = "sales", destination = "sales@example.com", isActive = true };

        var strangersUpdate = await stranger.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/email-forwarding/{ruleId}", updateBody);
        strangersUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersUpdate)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersUpdate = await owner.PutAsJsonAsync(
            $"/api/me/domains/{domainId}/email-forwarding/{ruleId}", updateBody);
        ownersUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/email-forwarding/1",
            new { source = "sales", destination = "sales@example.com", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateEmailForwardingRule_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "updatemydomainemailforwardingrule-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/email-forwarding/1",
            new { source = "info", destination = "me@example.com", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
