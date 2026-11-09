using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>InitiateMyOutgoingTransfer</c> use case:
/// <c>POST /api/me/domains/{id}/epp</c>, which retrieves the EPP authorization code for an
/// outgoing transfer. The admin equivalent is <c>InitiateOutgoingTransfer</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InitiateMyOutgoingTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Starting an outgoing transfer refuses a stranger before the registrar is asked, and for
    /// the owner returns the EPP code <see cref="StubRegistrarProvider.GetEppCodeAsync"/> answers.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetEppCode_RefusesAStrangerAndAnswersTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "initiate-my-outgoing-transfer");

        var strangersAttempt = await stranger.PostAsync($"/api/me/domains/{domainId}/epp", null);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PostAsync($"/api/me/domains/{domainId}/epp", null);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ownersAttempt.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("eppCode").GetString().Should().Be("STUB-EPP-CODE");
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetEppCode_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}/epp", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InitiateOutgoingTransfer_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "initiatemyoutgoingtransfer-validation");

        var response = await client.PostAsync("/api/me/domains/0/epp", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
