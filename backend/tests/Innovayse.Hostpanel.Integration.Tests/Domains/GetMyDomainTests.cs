using System.Net;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>GetMyDomain</c> use case: <c>GET /api/me/domains/{id}</c>, the
/// client-facing read of a single domain by primary key, refused for anyone but its owner.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Reading somebody else's domain by id answers 404 with <c>DOMAIN_NOT_FOUND</c>, and the
    /// caller's own read of the same id succeeds, proving the refusal is about ownership, not
    /// about the route being broken.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_RefusesAStrangerAndServesTheOwnerAsync()
    {
        var (_, owner, domainId, stranger) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain");

        var strangersAttempt = await stranger.GetAsync($"/api/me/domains/{domainId}");
        strangersAttempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(strangersAttempt)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);

        var ownersAttempt = await owner.GetAsync($"/api/me/domains/{domainId}");
        ownersAttempt.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The client's own domain, read alone or in the list, carries none of the staff-only fields:
    /// the registrar's reference, the transfer code, the subscription id and the staff notes stay
    /// out though the record holds all four.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_CarriesNoRegistrarReferenceTransferCodeOrStaffNoteAsync()
    {
        var (_, owner, domainId, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-internals");
        var secrets = new[] { $"reg-ref-{Guid.NewGuid():N}", $"epp-{Guid.NewGuid():N}", $"sub-{Guid.NewGuid():N}", $"note-{Guid.NewGuid():N}" };

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Domains
                .Where(d => d.Id == domainId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.RegistrarRef, secrets[0])
                    .SetProperty(d => d.EppCode, secrets[1])
                    .SetProperty(d => d.SubscriptionId, secrets[2])
                    .SetProperty(d => d.AdminNotes, secrets[3]));
        }

        var single = await owner.GetStringAsync($"/api/me/domains/{domainId}");
        var list = await owner.GetStringAsync("/api/me/domains");

        foreach (var json in new[] { single, list })
        {
            foreach (var secret in secrets)
            {
                json.Should().NotContain(secret);
            }

            foreach (var field in new[] { "registrarRef", "eppCode", "subscriptionId", "adminNotes", "reminders", "clientId" })
            {
                json.Should().NotContain($"\"{field}\"");
            }
        }
    }

    /// <summary>An id nobody's account ever took answers exactly like a stranger's domain would.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_UnknownIdAnswersLikeAStrangersDomainAsync()
    {
        var (_, owner, _, _) = await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "get-my-domain-unknown");

        var response = await owner.GetAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/domains/{DomainsTestSeeds.UnknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetDomain_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "getmydomain-validation");

        var response = await client.GetAsync("/api/me/domains/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
