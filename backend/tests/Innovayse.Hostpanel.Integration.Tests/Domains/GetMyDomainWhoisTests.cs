using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>GetMyDomainWhois</c> use case:
/// <c>GET /api/me/domains/{id}/whois</c>. The admin equivalent is <c>GetWhois</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyDomainWhoisTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A WHOIS lookup on somebody else's domain refuses with <c>DOMAIN_NOT_FOUND</c> before the
    /// registrar is ever asked; on the owner's own domain the registrar is asked and, because
    /// <see cref="StubRegistrarProvider.GetWhoisAsync"/> always answers <see langword="null"/>,
    /// answers a <em>different</em> 404: <c>DOMAIN_WHOIS_NOT_FOUND</c>. The two land on the same
    /// status but are distinguishable by code, which is the point of a code at all.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetWhois_RefusesAStrangerAndAnswersTheRegistrarsOwnNotFoundForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-whois");

        var strangersAttempt = await stranger.GetAsync($"/api/me/domains/{domainId}/whois");
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.GetAsync($"/api/me/domains/{domainId}/whois");
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(ownersAttempt)).Should().Be(HostpanelErrorCodes.DOMAIN_WHOIS_NOT_FOUND);
    }

    /// <summary>
    /// The registrar is asked about the domain's full stored name (<c>label.com</c>) exactly as it
    /// is, never with the TLD appended a second time (<c>label.com.com</c>).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetWhois_AsksTheRegistrarForTheFullDomainNameAsync()
    {
        var (_, owner, domainId, _) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-whois-name");

        using var domainBody = JsonDocument.Parse(await owner.GetStringAsync($"/api/me/domains/{domainId}"));
        var storedName = domainBody.RootElement.GetProperty("name").GetString()!;

        await owner.GetAsync($"/api/me/domains/{domainId}/whois");

        StubRegistrarProvider.WhoisLookups.Should().Contain(storedName);
        StubRegistrarProvider.WhoisLookups.Should().NotContain(name => name.EndsWith(".com.com", StringComparison.Ordinal));
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetWhois_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}/whois");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetWhois_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "getmydomainwhois-validation");

        var response = await client.GetAsync("/api/me/domains/0/whois");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
