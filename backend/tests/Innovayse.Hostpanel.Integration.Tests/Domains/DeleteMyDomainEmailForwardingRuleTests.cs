using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>DeleteMyDomainEmailForwardingRule</c> use case:
/// <c>DELETE /api/me/domains/{id}/email-forwarding/{ruleId}</c>. The admin equivalent is
/// <c>DeleteEmailForwardingRule</c>; the sibling client-facing use cases in the same lifecycle
/// are <c>AddMyDomainEmailForwardingRule</c> and <c>UpdateMyDomainEmailForwardingRule</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteMyDomainEmailForwardingRuleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a rule refuses a stranger with <c>DOMAIN_NOT_FOUND</c> and round-trips for the owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteEmailForwardingRule_RefusesAStrangerAndRoundTripsForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "delete-my-domain-email-fwd-rule");
        await owner.PostAsJsonAsync(
            $"/api/me/domains/{domainId}/email-forwarding",
            new { source = "info", destination = "info@example.com" });

        var domainAfterAdd = await owner.GetAsync($"/api/me/domains/{domainId}");
        using var domainBody = JsonDocument.Parse(await domainAfterAdd.Content.ReadAsStringAsync());
        var ruleId = domainBody.RootElement.GetProperty("management").GetProperty("emailForwardingRules")[0].GetProperty("id").GetInt32();

        var strangersDelete = await stranger.DeleteAsync($"/api/me/domains/{domainId}/email-forwarding/{ruleId}");
        strangersDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersDelete)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersDelete = await owner.DeleteAsync($"/api/me/domains/{domainId}/email-forwarding/{ruleId}");
        ownersDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteEmailForwardingRule_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}/email-forwarding/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteEmailForwardingRule_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "deletemydomainemailforwardingrule-validation");

        var response = await client.DeleteAsync("/api/me/domains/0/email-forwarding/1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
