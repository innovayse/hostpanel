using System.Net;
using System.Net.Http.Json;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.ImportRegistrarDomain;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="ImportRegistrarDomainCommand"/> through
/// <c>POST /api/domains/registrar/{module}/import</c>: a domain a registrar account holds is stored for a client with the
/// registrar's own reference and expiry, a status that follows the expiry and the extension's renew price, and with no order,
/// invoice or charge.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ImportRegistrarDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A name unique to one test run, under a unique extension so no other test's TLD configuration prices it.</summary>
    /// <param name="label">A short label for readable failures.</param>
    /// <returns>A domain name.</returns>
    private static string UniqueName(string label) => $"{label}.{DomainsTestSeeds.UniqueTld()}";

    /// <summary>Reads the new domain's id off the <c>Location</c> link of a 201.</summary>
    /// <param name="response">The import's answer.</param>
    /// <returns>The domain's id.</returns>
    private static int IdOf(HttpResponseMessage response) => int.Parse(response.Headers.Location!.ToString().Split('/').Last());

    /// <summary>Seeds a client for the import to name.</summary>
    /// <returns>The client's id.</returns>
    private async Task<int> SeedClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>Reads the stored domain with the given id.</summary>
    /// <param name="id">The domain's id.</param>
    /// <returns>The domain.</returns>
    private async Task<RegisteredDomain> StoredAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Domains.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    /// <summary>
    /// A running domain the registrar holds is stored for the client as active with the registrar's reference and expiry, the
    /// due date following the expiry and the module set, and the client has no invoice and no order because of it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ARunningDomain_IsStoredWithTheRegistrarsReferenceAndExpiryAndNoChargeAsync()
    {
        var name = UniqueName("run");
        var expiry = DateTimeOffset.UtcNow.AddMonths(9);
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(
            factory, RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-RUN", expiry)));

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = IdOf(response);
        var domain = await StoredAsync(id);
        domain.ClientId.Should().Be(clientId);
        domain.Name.Should().Be(name);
        domain.Status.Should().Be(DomainStatus.Active);
        domain.RegistrarModule.Should().Be(TestRegistrarModules.First);
        domain.RegistrarRef.Should().Be("REF-RUN");
        domain.ExpiresAt.Should().BeCloseTo(expiry, TimeSpan.FromMilliseconds(1));
        domain.NextDueDate.Should().Be(domain.ExpiresAt);
        domain.FirstPaymentAmount.Should().Be(0m);
        domain.OrderId.Should().BeNull();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == clientId)).Should().BeFalse();
        (await context.ActivityLogs.AsNoTracking().AnyAsync(l => l.ClientId == clientId && l.Description.StartsWith("Imported Domain From Registrar")))
            .Should().BeTrue();
    }

    /// <summary>A domain whose expiry has passed is stored as expired, with its expiry as the registrar reports it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ALapsedDomain_IsStoredExpiredAsync()
    {
        var name = UniqueName("lapsed");
        var expiry = DateTimeOffset.UtcNow.AddDays(-20);
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(
            factory, RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-LAPSED", expiry)));

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var domain = await StoredAsync(IdOf(response));
        domain.Status.Should().Be(DomainStatus.Expired);
        domain.ExpiresAt.Should().BeCloseTo(expiry, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>Configures an extension of the test registrar with the given sell currency and renew prices.</summary>
    /// <param name="tld">The extension, without the leading dot.</param>
    /// <param name="sellCurrency">The ISO 4217 code the sell prices are set in.</param>
    /// <param name="renew">The renew sell prices keyed by period in years.</param>
    /// <returns>A task that completes when the extension is stored.</returns>
    private async Task SeedTldAsync(string tld, string sellCurrency, Dictionary<int, decimal> renew)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var config = TldConfig.Create(tld, TestRegistrarModules.First, sellCurrency, sellCurrency, true, 0, []);
        config.UpdateSellPrices([], [], renew);
        context.TldConfigs.Add(config);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads the code of the store's base currency, the one a client with no recorded currency is billed in.</summary>
    /// <returns>The ISO 4217 code of the base currency.</returns>
    private async Task<string> BaseCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return (await context.Currencies.AsNoTracking().SingleAsync(c => c.IsBase)).Code;
    }

    /// <summary>Imports one domain the test registrar lists and reads the stored row back.</summary>
    /// <param name="name">The domain's name.</param>
    /// <returns>The stored domain.</returns>
    private async Task<RegisteredDomain> ImportListedAsync(string name)
    {
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(
            factory,
            RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-PRICE", DateTimeOffset.UtcNow.AddYears(1))));

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await StoredAsync(IdOf(response));
    }

    /// <summary>The recurring amount is the extension's one year renew price, in the client's billing currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_FollowsTheExtensionsRenewPriceAsync()
    {
        var tld = DomainsTestSeeds.UniqueTld();
        var currency = await BaseCurrencyAsync();
        await SeedTldAsync(tld, currency, new Dictionary<int, decimal> { [1] = 14.5m, [2] = 28m });

        var domain = await ImportListedAsync($"priced.{tld}");

        domain.RecurringAmount.Should().Be(14.5m);
        domain.PriceCurrency.Should().Be(currency);
    }

    /// <summary>An extension that sets no renew price for the year leaves the recurring amount at zero, and the domain is still imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_AnExtensionWithNoOneYearRenewPrice_ImportsWithNoRecurringAmountAsync()
    {
        var tld = DomainsTestSeeds.UniqueTld();
        await SeedTldAsync(tld, await BaseCurrencyAsync(), new Dictionary<int, decimal> { [2] = 28m });

        var domain = await ImportListedAsync($"twoyears.{tld}");

        domain.RecurringAmount.Should().Be(0m);
    }

    /// <summary>An extension priced in a currency with no exchange rate leaves the recurring amount at zero, and the domain is still imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_AnExtensionPricedInAnUnratedCurrency_ImportsWithNoRecurringAmountAsync()
    {
        var tld = DomainsTestSeeds.UniqueTld();
        await SeedTldAsync(tld, "XXX", new Dictionary<int, decimal> { [1] = 14.5m });

        var domain = await ImportListedAsync($"unrated.{tld}");

        domain.RecurringAmount.Should().Be(0m);
    }

    /// <summary>An extension with no configuration leaves the recurring amount at zero, and the domain is still imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_AnUnconfiguredExtension_ImportsWithNoRecurringAmountAsync()
    {
        var name = UniqueName("unpriced");
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(
            factory,
            RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-FREE", DateTimeOffset.UtcNow.AddYears(1))));

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await StoredAsync(IdOf(response))).RecurringAmount.Should().Be(0m);
    }

    /// <summary>The domain is found on a later page of the registrar's list, and the name is matched without regard to letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ADomainOnALaterPage_IsFoundAsync()
    {
        var name = UniqueName("later");
        var clientId = await SeedClientAsync();
        var plugin = RegistrarImportTestSeeds.PluginListing(cursor => cursor is null
            ? new RegistrarDomainPage([RegistrarImportTestSeeds.Listed(UniqueName("other"), "REF-OTHER", DateTimeOffset.UtcNow.AddYears(1))], "page-2")
            : new RegistrarDomainPage(
                [RegistrarImportTestSeeds.Listed(name.ToUpperInvariant(), "REF-LATER", DateTimeOffset.UtcNow.AddYears(1))], null));
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await StoredAsync(IdOf(response))).RegistrarRef.Should().Be("REF-LATER");
    }

    /// <summary>A name the registrar's list does not hold is answered 404 <c>REGISTRAR_DOMAIN_NOT_FOUND</c> and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ADomainTheRegistrarDoesNotHold_Returns404Async()
    {
        var name = UniqueName("absent");
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, RegistrarImportTestSeeds.PluginHolding());

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Domains.AsNoTracking().AnyAsync(d => d.Name == name)).Should().BeFalse();
    }

    /// <summary>A domain the registrar gives no expiry for is answered 409 <c>DOMAIN_IMPORT_EXPIRY_UNKNOWN</c> and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ADomainWithNoExpiry_Returns409Async()
    {
        var name = UniqueName("noexpiry");
        var clientId = await SeedClientAsync();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(
            factory, RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-NONE", null)));

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_IMPORT_EXPIRY_UNKNOWN);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Domains.AsNoTracking().AnyAsync(d => d.Name == name)).Should().BeFalse();
    }

    /// <summary>A name the panel already has, for whichever client, is answered 409 <c>DOMAIN_ALREADY_REGISTERED</c> and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_ADomainThePanelAlreadyHas_Returns409Async()
    {
        var name = UniqueName("held");
        int clientId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            await DomainsTestSeeds.SeedDomainAsync(context, clientId, name, DomainStatus.Active, TestRegistrarModules.First);
        }

        var plugin = RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-HELD", DateTimeOffset.UtcNow.AddYears(1)));
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.PostAsJsonAsync(
            $"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId = await SeedClientAsync(), domainName = name.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A client id that names no client is answered 404 <c>CLIENT_NOT_FOUND</c> and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_AnUnknownClient_Returns404Async()
    {
        var name = UniqueName("noclient");
        var plugin = RegistrarImportTestSeeds.PluginHolding(RegistrarImportTestSeeds.Listed(name, "REF-1", DateTimeOffset.UtcNow.AddYears(1)));
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.PostAsJsonAsync(
            $"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId = DomainsTestSeeds.UnknownId, domainName = name });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A malformed name, a missing client and an unknown module are refused with 400 before anything is read.</summary>
    /// <param name="module">The registrar module in the route.</param>
    /// <param name="clientId">The client id in the body.</param>
    /// <param name="domainName">The domain name in the body.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("registrar-one", 1, "not a domain")]
    [InlineData("registrar-one", 1, "example.123")]
    [InlineData("registrar-one", 0, "example.com")]
    [InlineData("no-such-registrar", 1, "example.com")]
    public async Task Import_AnInvalidRequest_Returns400Async(string module, int clientId, string domainName)
    {
        var plugin = RegistrarImportTestSeeds.PluginHolding();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.PostAsJsonAsync($"/api/domains/registrar/{module}/import", new { clientId, domainName });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A caller without a token is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_WithoutToken_Returns401Async()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId = 1, domainName = "example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client account that is not staff is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Import_AsAClientUser_Returns403Async()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "import-registrar-domain");

        var response = await client.PostAsJsonAsync(
            $"/api/domains/registrar/{TestRegistrarModules.First}/import", new { clientId = 1, domainName = "example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
