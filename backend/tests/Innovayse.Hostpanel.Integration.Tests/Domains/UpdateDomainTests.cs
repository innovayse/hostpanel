using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>UpdateDomain</c> use case:
/// <c>PUT /api/domains/{id}</c>, updating billing and administrative fields on a domain.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a valid update body for the given target status.</summary>
    /// <param name="status">The <c>DomainStatus</c> name to set.</param>
    /// <returns>The anonymous body to PUT.</returns>
    private static object ValidBody(string status) => new
    {
        firstPaymentAmount = 10m,
        recurringAmount = 12m,
        paymentMethod = "Credit/Debit Card",
        promotionCode = (string?)null,
        subscriptionId = (string?)null,
        adminNotes = "Updated by test",
        expiresAt = DateTimeOffset.UtcNow.AddYears(1).ToString("O"),
        nextDueDate = DateTimeOffset.UtcNow.AddMonths(11).ToString("O"),
        registrationPeriod = 1,
        status,
        nameservers = Array.Empty<string>(),
    };

    /// <summary>Updating an existing domain succeeds and persists the new admin notes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDomain_ExistingDomain_Returns204AndPersistsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PutAsJsonAsync($"/api/domains/{domainId}", ValidBody(nameof(DomainStatus.Active)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None);
        reloaded.AdminNotes.Should().Be("Updated by test");
    }

    /// <summary>An unrecognized status name is refused with 400 when the body is bound to <see cref="DomainStatus"/>, before any handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDomain_WithInvalidStatus_Returns400Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PutAsJsonAsync($"/api/domains/{domainId}", ValidBody("not-a-status"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unknown domain id answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDomain_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/domains/{DomainsTestSeeds.UnknownId}", ValidBody(nameof(DomainStatus.Active)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateDomain_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/domains/{DomainsTestSeeds.UnknownId}", ValidBody(nameof(DomainStatus.Active)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
