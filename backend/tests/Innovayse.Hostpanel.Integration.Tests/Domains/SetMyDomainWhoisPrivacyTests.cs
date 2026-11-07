using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>SetMyDomainWhoisPrivacy</c> use case:
/// <c>PUT /api/me/domains/{id}/whois-privacy</c>. The admin equivalent is <c>SetWhoisPrivacy</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetMyDomainWhoisPrivacyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>WHOIS privacy toggling refuses a stranger's attempt and applies for the owner.</summary>
    [Fact]
    public async Task SetWhoisPrivacy_RefusesAStrangerAndAppliesForTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "set-my-domain-whois-privacy");
        var body = new Dictionary<string, bool> { ["value"] = true };

        var strangersAttempt = await stranger.PutAsJsonAsync($"/api/me/domains/{domainId}/whois-privacy", body);
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/whois-privacy", body);
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    [Fact]
    public async Task SetWhoisPrivacy_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/whois-privacy", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    [Fact]
    public async Task SetWhoisPrivacy_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "setmydomainwhoisprivacy-validation");

        var response = await client.PutAsJsonAsync(
            "/api/me/domains/0/whois-privacy",
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// The body field is <c>value</c>: <c>{ value: true }</c> switches WHOIS privacy on and <c>{ value: false }</c>
    /// switches it off, read back through <c>GET /api/domains/{id}</c>.
    /// </summary>
    [Fact]
    public async Task SetWhoisPrivacy_ValueField_TogglesTheFlagAsync()
    {
        var (admin, owner, domainId, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "whois-privacy-value");

        (await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/whois-privacy", new { value = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadFlagAsync(admin, domainId)).Should().BeTrue();

        (await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/whois-privacy", new { value = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadFlagAsync(admin, domainId)).Should().BeFalse();
    }

    /// <summary>
    /// A body that names the flag <c>enabled</c> instead of <c>value</c>, or has no fields at all, is refused with 400
    /// <c>VALIDATION_ERROR</c> and never read as <c>false</c>: a domain whose WHOIS privacy is on keeps it.
    /// </summary>
    /// <param name="body">The JSON body the client sends.</param>
    [Theory]
    [InlineData("{\"enabled\":true}")]
    [InlineData("{\"enabled\":false}")]
    [InlineData("{}")]
    public async Task SetWhoisPrivacy_WithoutValue_Returns400AndKeepsTheFlagAsync(string body)
    {
        var (admin, owner, domainId, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "whois-privacy-novalue");
        (await owner.PutAsJsonAsync($"/api/me/domains/{domainId}/whois-privacy", new { value = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadFlagAsync(admin, domainId)).Should().BeTrue();

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await owner.PutAsync($"/api/me/domains/{domainId}/whois-privacy", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        (await ReadFlagAsync(admin, domainId)).Should().BeTrue();
    }

    /// <summary>Reads the WHOIS privacy switch of a domain through the admin detail route.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="domainId">The domain's primary key.</param>
    /// <returns>The current WHOIS privacy flag.</returns>
    private static async Task<bool> ReadFlagAsync(HttpClient admin, int domainId)
    {
        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/domains/{domainId}");
        return body.GetProperty("features").GetProperty("whoisPrivacy").GetBoolean();
    }
}
