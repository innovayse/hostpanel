using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>GetMyDomainContact</c> use case:
/// <c>GET /api/me/domains/{id}/contact</c>. The admin equivalent is <c>GetDomainContact</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyDomainContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A stranger is refused with <c>DOMAIN_NOT_FOUND</c> before the registrar is asked; the owner
    /// receives the registrant contact <see cref="StubRegistrarProvider"/> holds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_RefusesAStrangerAndReturnsTheRegistrantForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-contact");

        var strangersAttempt = await stranger.GetAsync($"/api/me/domains/{domainId}/contact");
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.GetAsync($"/api/me/domains/{domainId}/contact");
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await ownersAttempt.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("firstName").GetString().Should().Be(StubRegistrarProvider.StubContact.FirstName);
        body.RootElement.GetProperty("email").GetString().Should().Be(StubRegistrarProvider.StubContact.Email);
        body.RootElement.GetProperty("country").GetString().Should().Be(StubRegistrarProvider.StubContact.Country);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}/contact");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (admin, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "getmydomaincontact-validation");

        var response = await admin.GetAsync("/api/me/domains/0/contact");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
