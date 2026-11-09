using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>TransferMyDomain</c> use case:
/// <c>POST /api/me/domains/transfer-order</c>, which places an order to transfer a domain in to
/// the caller's own account. The admin equivalent is <c>TransferDomain</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TransferMyDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// With no active domain product configured, a transfer-in order has nothing to sell itself
    /// as: 503 with <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c>, and the caller is told rather than
    /// handed a 500. Fails if the refusal is removed and the handler is allowed to throw instead.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferMyDomain_WithNoDomainProductConfigured_RefusesServiceUnavailableAsync()
    {
        var (owner, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "transfer-my-domain");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var domainProducts = await db.Products
                .Where(p => p.Status == ProductStatus.Active && p.Type == ProductType.Domain)
                .ToListAsync();
            foreach (var product in domainProducts)
            {
                product.Deactivate();
            }

            await db.SaveChangesAsync();
        }

        var response = await owner.PostAsJsonAsync(
            "/api/me/domains/transfer-order",
            new { domainName = "incoming-transfer.com", eppCode = "EPP-SECRET-CODE", years = 1, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_PRODUCT_NOT_CONFIGURED);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferMyDomain_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/me/domains/transfer-order",
            new { domainName = "incoming-transfer.com", eppCode = "EPP-SECRET-CODE", years = 1, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An authorisation code shorter than six characters is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferMyDomain_ShortEppCode_Returns400ValidationErrorAsync()
    {
        var owner = await DomainsTestSeeds.RegisterOwnerAsync(factory, "transfer-my-domain-invalid");

        var response = await owner.Client.PostAsJsonAsync(
            "/api/me/domains/transfer-order",
            new { domainName = "incoming-transfer.com", eppCode = "abc", years = 1, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
