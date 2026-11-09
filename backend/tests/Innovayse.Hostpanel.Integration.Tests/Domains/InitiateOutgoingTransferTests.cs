using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>InitiateOutgoingTransfer</c> use case:
/// <c>POST /api/domains/{id}/initiate-outgoing-transfer</c>, retrieving the EPP code for an
/// outgoing transfer. The client-facing equivalent is <c>InitiateMyOutgoingTransfer</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InitiateOutgoingTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Initiating an outgoing transfer for an active, registrar-linked domain succeeds and
    /// returns the (stubbed) EPP code.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InitiateOutgoingTransfer_ActiveDomain_Returns200WithEppCodeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsync($"/api/domains/{domainId}/initiate-outgoing-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("eppCode").GetString().Should().Be("STUB-EPP-CODE");
    }

    /// <summary>A domain with no registrar reference answers 400 with <c>DOMAIN_NO_REGISTRAR_REF</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InitiateOutgoingTransfer_NoRegistrarRef_Returns400Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsync($"/api/domains/{domain.Id}/initiate-outgoing-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
    }

    /// <summary>An unknown domain id answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InitiateOutgoingTransfer_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/initiate-outgoing-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InitiateOutgoingTransfer_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/initiate-outgoing-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
