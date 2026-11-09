using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the registrar a domain operation is sent to: with two registrars
/// configured, each TLD's domains are registered, renewed and managed at the registrar that TLD
/// names and never at the other one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RegistrarRoutingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The registrar the first TLD is configured for.</summary>
    private readonly RecordingRegistrar _nameAm = new();

    /// <summary>The registrar the second TLD is configured for.</summary>
    private readonly RecordingRegistrar _namecheap = new();

    /// <summary>
    /// Gets a TLD extension unique to this test instance, so configurations never collide in the shared
    /// database. It is letters only, as every TLD is: each hex digit of a new id becomes one of the
    /// letters a to p, so a registration of a name under it passes the domain name rule.
    /// </summary>
    /// <param name="prefix">A short letter prefix that keeps the two TLDs of one test apart.</param>
    /// <returns>The extension, without the leading dot.</returns>
    private static string UniqueTld(string prefix) =>
        prefix + string.Concat(Guid.NewGuid().ToString("N").Take(9).Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));

    /// <summary>
    /// Configures one TLD for each registrar and builds a host whose factory answers with the
    /// recording registrars.
    /// </summary>
    /// <returns>An admin client against that host and the two TLD extensions (the first TLD belongs to the first registrar).</returns>
    private async Task<(HttpClient Admin, string NameAmTld, string NamecheapTld)> ConfigureTwoRegistrarsAsync()
    {
        var nameAmTld = UniqueTld("a");
        var namecheapTld = UniqueTld("c");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.TldConfigs.Add(TldConfig.Create(nameAmTld, RegistrarModule.NameAm, "USD", "USD", true, 1, []));
            context.TldConfigs.Add(TldConfig.Create(namecheapTld, RegistrarModule.Namecheap, "USD", "USD", true, 2, []));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var registrars = new FakeRegistrarProviderFactory();
        registrars.Register(RegistrarModule.NameAm, _nameAm);
        registrars.Register(RegistrarModule.Namecheap, _namecheap);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IRegistrarProviderFactory>(registrars)));

        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return (admin, nameAmTld, namecheapTld);
    }

    /// <summary>Seeds an active domain with a registrar reference under the given TLD.</summary>
    /// <param name="tld">The extension the domain ends in.</param>
    /// <returns>The domain's name and primary key.</returns>
    private async Task<(string Name, int Id)> SeedDomainAsync(string tld)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}"[..12] + $".{tld}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-ROUTING", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return (domain.Name, domain.Id);
    }

    /// <summary>A renewal goes to the registrar the domain's TLD is configured for, and only to it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Renew_DomainsOfTwoTlds_EachGoesToItsOwnRegistrarAsync()
    {
        var (admin, nameAmTld, namecheapTld) = await ConfigureTwoRegistrarsAsync();
        var first = await SeedDomainAsync(nameAmTld);
        var second = await SeedDomainAsync(namecheapTld);

        (await admin.PostAsJsonAsync($"/api/domains/{first.Id}/renew", new { years = 1 })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/domains/{second.Id}/renew", new { years = 1 })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        _nameAm.Calls.Should().Contain($"RenewAsync {first.Name}").And.NotContain($"RenewAsync {second.Name}");
        _namecheap.Calls.Should().Contain($"RenewAsync {second.Name}").And.NotContain($"RenewAsync {first.Name}");
    }

    /// <summary>A flag change on an existing domain goes to the registrar the domain's TLD is configured for, and only to it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetAutoRenew_DomainsOfTwoTlds_EachGoesToItsOwnRegistrarAsync()
    {
        var (admin, nameAmTld, namecheapTld) = await ConfigureTwoRegistrarsAsync();
        var first = await SeedDomainAsync(nameAmTld);
        var second = await SeedDomainAsync(namecheapTld);

        (await admin.PutAsJsonAsync($"/api/domains/{first.Id}/auto-renew", new { value = true })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"/api/domains/{second.Id}/auto-renew", new { value = true })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        _nameAm.Calls.Should().Contain($"SetAutoRenewAsync {first.Name}").And.NotContain($"SetAutoRenewAsync {second.Name}");
        _namecheap.Calls.Should().Contain($"SetAutoRenewAsync {second.Name}").And.NotContain($"SetAutoRenewAsync {first.Name}");
    }

    /// <summary>
    /// A domain registered at one registrar keeps going there after its TLD is configured for the
    /// other one: renewals and DNS changes reach the original registrar and never the new one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Operations_TldRegistrarChangedAfterRegistration_StillGoToTheOriginalRegistrarAsync()
    {
        var (admin, nameAmTld, _) = await ConfigureTwoRegistrarsAsync();
        int clientId;
        using (var scope = factory.Services.CreateScope())
        {
            clientId = await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        var name = $"{Guid.NewGuid():N}"[..12] + $".{nameAmTld}";
        var registered = await admin.PostAsJsonAsync("/api/domains/register", new
        {
            domainName = name,
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = false,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        int domainId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var config = await context.TldConfigs.FirstAsync(x => x.Tld == nameAmTld, CancellationToken.None);
            config.Update(RegistrarModule.Namecheap, config.Currency, config.SellCurrency, config.IsEnabled, config.SortOrder, []);
            domainId = (await context.Domains.FirstAsync(x => x.Name == name, CancellationToken.None)).Id;
            await context.SaveChangesAsync(CancellationToken.None);
        }

        (await admin.PostAsJsonAsync($"/api/domains/{domainId}/renew", new { years = 1 })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"/api/domains/{domainId}/auto-renew", new { value = true })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        _nameAm.Calls.Should().Contain($"RenewAsync {name}").And.Contain($"SetAutoRenewAsync {name}");
        _namecheap.Calls.Should().NotContain(call => call.Contains(name));
    }

    /// <summary>A new registration is placed at the registrar the name's TLD is configured for, and only there.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_NewDomain_IsPlacedAtTheRegistrarOfItsTldAsync()
    {
        var (admin, _, namecheapTld) = await ConfigureTwoRegistrarsAsync();
        int clientId;
        using (var scope = factory.Services.CreateScope())
        {
            clientId = await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        var name = $"{Guid.NewGuid():N}"[..12] + $".{namecheapTld}";
        var response = await admin.PostAsJsonAsync("/api/domains/register", new
        {
            domainName = name,
            years = 1,
            clientId,
            whoisPrivacy = false,
            autoRenew = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _namecheap.Calls.Should().Contain($"RegisterAsync {name}");
        _nameAm.Calls.Should().NotContain(call => call.Contains(name));
    }
}
