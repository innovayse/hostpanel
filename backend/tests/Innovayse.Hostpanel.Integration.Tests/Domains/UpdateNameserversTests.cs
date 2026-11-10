using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>UpdateNameservers</c> use case:
/// <c>PUT /api/domains/{id}/nameservers</c>. The client-facing equivalent is
/// <c>UpdateMyDomainNameservers</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateNameserversTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// <c>PUT /api/domains/{id}/nameservers</c> with <c>{ nameservers: ["ns1", "ns2"] }</c>, the exact
    /// shape <c>ClientDomainDetailView.vue</c> sends, succeeds with two entries.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithTwoEntries_Returns204Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PutAsJsonAsync(
            $"/api/domains/{domainId}/nameservers",
            new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Fewer than two nameservers refuses with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithOneEntry_Returns400Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PutAsJsonAsync(
            $"/api/domains/{domainId}/nameservers", new { nameservers = new[] { "ns1.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateNameservers_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/nameservers",
            new { nameservers = new[] { "ns1.example.com", "ns2.example.com" } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
