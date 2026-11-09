using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>SetWhoisPrivacy</c> use case:
/// <c>PUT /api/domains/{id}/whois-privacy</c>. The client-facing equivalent is
/// <c>SetMyDomainWhoisPrivacy</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetWhoisPrivacyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// <c>PUT /api/domains/{id}/whois-privacy</c> with <c>{ enabled: true }</c>, the exact body
    /// <c>domainsStore.ts</c>'s <c>setWhoisPrivacy</c> sends, succeeds and persists the flag.
    /// <see cref="SetWhoisPrivacyCommand"/> names its boolean <c>Enabled</c> to match this wire shape.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetWhoisPrivacy_WithEnabledField_Returns204AndPersistsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PutAsJsonAsync($"/api/domains/{domainId}/whois-privacy", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None);
        reloaded.WhoisPrivacy.Should().BeTrue();
    }

    /// <summary>An unknown domain id answers 404 rather than binding and silently doing nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetWhoisPrivacy_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/whois-privacy", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetWhoisPrivacy_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/whois-privacy", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive domain id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetWhoisPrivacy_NonPositiveDomainId_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync("/api/domains/0/whois-privacy", new { enabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
