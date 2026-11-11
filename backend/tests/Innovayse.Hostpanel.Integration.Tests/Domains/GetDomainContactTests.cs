using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetDomainContact;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>GetDomainContact</c> use case:
/// <c>GET /api/domains/{id}/contact</c>. The client-facing equivalent is <c>GetMyDomainContact</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDomainContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registered domain answers 200 with the contact the registrar holds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_AfterRegister_Returns200WithTheRegistrantAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "get-domain-contact");
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, clientId);

        var response = await admin.GetAsync($"/api/domains/{domainId}/contact");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(StubRegistrarProvider.StubContact.Email);
    }

    /// <summary>An id no domain ever took answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_UnknownId_Returns404Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/contact");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>
    /// A domain the registrar holds no contact for (the stub's reserved <c>.test</c> suffix) fails
    /// with <c>DOMAIN_CONTACT_NOT_FOUND</c>, dispatched through the real bus.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_RegistrarHoldsNoContact_FailsWithDomainContactNotFoundAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var outcome = await bus.InvokeAsync<Result<DomainContactDto>>(
            new GetDomainContactQuery(domainId), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_CONTACT_NOT_FOUND);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/domains/{DomainsTestSeeds.UnknownId}/contact");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c> before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetContact_WithNonPositiveId_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/domains/0/contact");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
