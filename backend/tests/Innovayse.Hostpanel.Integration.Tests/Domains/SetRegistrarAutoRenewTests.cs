using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>SetRegistrarAutoRenew</c> use case:
/// <c>PUT /api/domains/{id}/registrar-auto-renew</c>, which changes auto renew at the registrar only. The panel's own
/// renewal flag is a separate setting, owned by <c>SetAutoRenew</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetRegistrarAutoRenewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Turning auto renew off at the registrar asks the registrar and leaves the panel's renewal flag on: the panel
    /// keeps renewing a domain whose registrar auto renew is off.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_Off_CallsTheRegistrarAndKeepsThePanelFlagAsync()
    {
        var registrar = NewRegistrar();
        var (admin, domain) = await SeedAsync(registrar, panelAutoRenew: true);

        var response = await admin.PutAsJsonAsync($"/api/domains/{domain.Id}/registrar-auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        registrar.Calls.Should().Equal($"SetAutoRenewAsync {domain.Name}");
        (await ReadPanelFlagAsync(domain.Id)).Should().BeTrue();
    }

    /// <summary>Turning auto renew on at the registrar does not switch the panel's renewal flag on.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_On_DoesNotChangeAPanelFlagThatIsOffAsync()
    {
        var registrar = NewRegistrar();
        var (admin, domain) = await SeedAsync(registrar, panelAutoRenew: false);

        var response = await admin.PutAsJsonAsync($"/api/domains/{domain.Id}/registrar-auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        registrar.Calls.Should().Equal($"SetAutoRenewAsync {domain.Name}");
        (await ReadPanelFlagAsync(domain.Id)).Should().BeFalse();
    }

    /// <summary>A registrar that rejects the change answers 502 and the panel flag stays as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_RegistrarRejects_Returns502AndKeepsThePanelFlagAsync()
    {
        var registrar = NewRegistrar(new RegistrarResult(false, null, null, null));
        var (admin, domain) = await SeedAsync(registrar, panelAutoRenew: true);

        var response = await admin.PutAsJsonAsync($"/api/domains/{domain.Id}/registrar-auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_AUTO_RENEW_UPSTREAM_FAILED);
        (await ReadPanelFlagAsync(domain.Id)).Should().BeTrue();
    }

    /// <summary>A domain never placed at a registrar has nothing to change there: 400 and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_DomainWithoutRegistrarRef_Returns400AndAsksNoRegistrarAsync()
    {
        var registrar = NewRegistrar();
        var host = HostWith(registrar);
        int domainId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            domainId = await DomainsTestSeeds.SeedDomainAsync(
                context, clientId, $"{Guid.NewGuid():N}.test", DomainStatus.Active, registrarModule: null);
        }

        var response = await (await AdminAsync(host)).PutAsJsonAsync(
            $"/api/domains/{domainId}/registrar-auto-renew", new { value = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        registrar.Calls.Should().BeEmpty();
    }

    /// <summary>A change writes one activity log line on the owning client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_Change_RecordsAnActivityLogLineAsync()
    {
        var registrar = NewRegistrar();
        var (admin, domain) = await SeedAsync(registrar, panelAutoRenew: true);

        (await admin.PutAsJsonAsync($"/api/domains/{domain.Id}/registrar-auto-renew", new { value = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var lines = await context.ActivityLogs.AsNoTracking()
            .Where(l => l.ClientId == domain.ClientId && l.Description.StartsWith("Set Domain Registrar Auto Renew"))
            .Select(l => l.Description)
            .ToListAsync();
        lines.Should().Equal($"Set Domain Registrar Auto Renew - Domain ID: {domain.Id}, Registrar Auto Renew: False");
    }

    /// <summary>A body that omits <c>value</c> is refused with 400 and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_EmptyBody_Returns400AndAsksNoRegistrarAsync()
    {
        var registrar = NewRegistrar();
        var (admin, domain) = await SeedAsync(registrar, panelAutoRenew: true);

        var response = await admin.PutAsJsonAsync($"/api/domains/{domain.Id}/registrar-auto-renew", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        registrar.Calls.Should().BeEmpty();
    }

    /// <summary>An id no domain ever took answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_UnknownDomain_Returns404Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/registrar-auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>A client token is refused with 403: the registrar's auto renew is staff only, and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_ClientToken_Returns403AndAsksNoRegistrarAsync()
    {
        var registrar = NewRegistrar();
        var host = HostWith(registrar);
        var (owner, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "registrar-auto-renew-client");
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/registrar-auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        registrar.Calls.Should().BeEmpty();
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetRegistrarAutoRenew_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/registrar-auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Builds a recording registrar that answers every operation with the stub's success.</summary>
    /// <param name="setAutoRenewAnswer">The scripted answer to an auto renew change, or null for the stub's.</param>
    /// <returns>The registrar a test hands to the host and reads its calls from.</returns>
    private static RecordingRegistrar NewRegistrar(RegistrarResult? setAutoRenewAnswer = null)
    {
        return new RecordingRegistrar(new ManualTimeProvider(TestMoments.Seeded)) { SetAutoRenewAnswer = setAutoRenewAnswer };
    }

    /// <summary>Builds a host whose registrar is the given recording one.</summary>
    /// <param name="registrar">The registrar every domain is managed through in this host.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWith(RecordingRegistrar registrar)
    {
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRegistrarProvider>(registrar)));
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

    /// <summary>
    /// Seeds an active domain placed at a registrar, with the given panel renewal flag, in a host that uses the given
    /// registrar.
    /// </summary>
    /// <param name="registrar">The registrar the host manages the domain through.</param>
    /// <param name="panelAutoRenew">The panel's own renewal flag the domain starts with.</param>
    /// <returns>An admin client of that host and the seeded domain.</returns>
    private async Task<(HttpClient Admin, RegisteredDomain Domain)> SeedAsync(RecordingRegistrar registrar, bool panelAutoRenew)
    {
        var host = HostWith(registrar);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, TestMoments.Seeded.AddYears(1),
            autoRenew: panelAutoRenew, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-REGISTRAR-AUTO-RENEW", domain.ExpiresAt, TestMoments.Seeded);
        await context.SaveChangesAsync(CancellationToken.None);
        return (await AdminAsync(host), domain);
    }

    /// <summary>Reads the panel's own renewal flag straight from the database.</summary>
    /// <param name="domainId">The domain's primary key.</param>
    /// <returns>The stored panel renewal flag.</returns>
    private async Task<bool> ReadPanelFlagAsync(int domainId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None)).AutoRenew;
    }
}
