using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.ModifyDomainContact;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>ModifyDomainContact</c> use case:
/// <c>POST /api/domains/{id}/modify-contact</c>. The client-facing equivalent is
/// <c>ModifyMyDomainContact</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ModifyDomainContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// <c>POST /api/domains/{id}/modify-contact</c> with the flat body <c>DomainContactModal.vue</c>
    /// emits, with no nested <c>contact</c> wrapper, succeeds. <see cref="ModifyDomainContactCommand"/> is flattened to
    /// these top-level fields for exactly this wire.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_WithFlatBody_Returns204Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/domains/{domainId}/modify-contact", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            organization = "",
            email = "ada@example.test",
            phone = "+10000000000",
            address1 = "1 Analytical Engine Way",
            address2 = "",
            city = "London",
            state = "London",
            postalCode = "00000",
            country = "GB",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A missing required contact field (email) refuses with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_MissingEmail_Returns400Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/domains/{domainId}/modify-contact", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            organization = "",
            email = "",
            phone = "+10000000000",
            address1 = "1 Analytical Engine Way",
            address2 = "",
            city = "London",
            state = "London",
            postalCode = "00000",
            country = "GB",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/modify-contact", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            organization = "",
            email = "ada@example.test",
            phone = "+10000000000",
            address1 = "1 Analytical Engine Way",
            address2 = "",
            city = "London",
            state = "London",
            postalCode = "00000",
            country = "GB",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Contacts live only at the registrar, so a domain never placed at one is refused with
    /// <c>DOMAIN_NO_REGISTRAR_REF</c> instead of reporting a change nobody made.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyContact_WithoutRegistrarRef_Returns400Async()
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
        var response = await client.PostAsJsonAsync($"/api/domains/{domain.Id}/modify-contact", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            organization = "",
            email = "ada@example.test",
            phone = "+10000000000",
            address1 = "1 Analytical Engine Way",
            address2 = "",
            city = "London",
            state = "London",
            postalCode = "00000",
            country = "GB",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
    }
}
