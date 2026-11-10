using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>RegisterDomain</c> use case:
/// <c>POST /api/domains/register</c>, admin registration of a new domain for a client.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RegisterDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Admin POSTs to <c>/api/domains/register</c> with a valid body. Expects 201 Created and a
    /// Location header pointing to the new domain.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_ValidRequest_Returns201Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "register-domain");

        var domainName = $"test-{Guid.NewGuid():N}";
        var response = await admin.PostAsJsonAsync("/api/domains/register", new
        {
            // The action binds RegisterDomainCommand itself, so the body carries the command's
            // member names; the registrant block a deleted request record once required is
            // never read.
            domainName = $"{domainName}.com",
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    /// <summary>An unauthenticated caller is refused with 401 before anything is registered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/domains/register", new
        {
            domainName = $"{Guid.NewGuid():N}.com",
            years = 1,
            clientId = 1,
            whoisPrivacy = false,
            autoRenew = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A period outside one to ten years is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_YearsOutOfRange_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/domains/register", new
        {
            domainName = "valid-name.com",
            years = 0,
            clientId = 1,
            whoisPrivacy = false,
            autoRenew = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// A registration paid in a currency other than the base keeps that currency on the domain,
    /// so its recorded prices are not read as base-currency amounts.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_WithPriceCurrency_StoresThatCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domainName = $"paid-{Guid.NewGuid():N}.com";

        var outcome = await bus.InvokeAsync<Result<int>>(new RegisterDomainCommand(
            clientId, domainName, 1, WhoisPrivacy: false, AutoRenew: true, Nameserver1: null, Nameserver2: null,
            FirstPaymentAmount: 5000m, RecurringAmount: 5000m, PriceCurrency: "AMD"));

        outcome.IsSuccess.Should().BeTrue();
        var domain = await context.Domains.AsNoTracking().SingleAsync(d => d.Id == outcome.Value);
        domain.PriceCurrency.Should().Be("AMD");
        domain.RecurringAmount.Should().Be(5000m);
    }
}
