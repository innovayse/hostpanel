using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>
    /// An <c>orderId</c> in the body is never bound: only order fulfilment links a domain to an order, so a staff call
    /// cannot make a domain read as created by an order (which a retry of that order's line would count as done).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_OrderIdInBody_IsIgnoredAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "register-domain-order");

        var domainName = $"order-{Guid.NewGuid():N}.com";
        var response = await admin.PostAsJsonAsync("/api/domains/register", new
        {
            domainName,
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = false,
            orderId = 424242,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domain = await context.Domains.AsNoTracking().SingleAsync(d => d.Name == domainName);
        domain.OrderId.Should().BeNull();
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

    /// <summary>
    /// A registration with the panel's renewal switched on sends the registrar auto renew off: the panel flag and the
    /// registrar's setting are separate, so a domain the panel renews is not also renewed by the registrar.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_PanelAutoRenew_SendsRegistrarAutoRenewOffAsync()
    {
        var registrar = new RecordingRegistrar(new ManualTimeProvider(TestMoments.Seeded));
        var (host, clientId) = await HostWithAsync(registrar, "register-panel-renew");
        var domainName = $"panel-{Guid.NewGuid():N}.com";

        var response = await (await AdminAsync(host)).PostAsJsonAsync("/api/domains/register", new
        {
            domainName,
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        registrar.Registrations.Should().ContainSingle().Which.AutoRenew.Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Domains.AsNoTracking().SingleAsync(d => d.Name == domainName)).AutoRenew.Should().BeTrue();
    }

    /// <summary>
    /// Registrar auto renew is switched on only when the operator asks for it with <c>registrarAutoRenew</c>, and the
    /// panel's renewal flag stays as sent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_RegistrarAutoRenewAsked_SendsItOnAndLeavesThePanelFlagAsync()
    {
        var registrar = new RecordingRegistrar(new ManualTimeProvider(TestMoments.Seeded));
        var (host, clientId) = await HostWithAsync(registrar, "register-registrar-renew");
        var domainName = $"registrar-{Guid.NewGuid():N}.com";

        var response = await (await AdminAsync(host)).PostAsJsonAsync("/api/domains/register", new
        {
            domainName,
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = false,
            registrarAutoRenew = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        registrar.Registrations.Should().ContainSingle().Which.AutoRenew.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Domains.AsNoTracking().SingleAsync(d => d.Name == domainName)).AutoRenew.Should().BeFalse();
    }

    /// <summary>Builds a host whose registrar is the given recording one, with a client to register a domain for.</summary>
    /// <param name="registrar">The registrar every registration in this host goes through.</param>
    /// <param name="label">A short label folded into the client's unique email.</param>
    /// <returns>The host and the id of the client.</returns>
    private async Task<(WebApplicationFactory<Program> Host, int ClientId)> HostWithAsync(RecordingRegistrar registrar, string label)
    {
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, label);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRegistrarProvider>(registrar)));
        return (host, clientId);
    }

    /// <summary>Creates a client of the host authenticated as an admin.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <returns>The authenticated client.</returns>
    private async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> host)
    {
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return admin;
    }
}
