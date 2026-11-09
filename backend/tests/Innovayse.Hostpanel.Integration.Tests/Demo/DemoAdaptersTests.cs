using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Time;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo fakes themselves: each outward operation succeeds with no network, and the identifiers
/// they hand back depend only on the input, so two calls with the same input agree.
/// </summary>
public sealed class DemoAdaptersTests
{
    /// <summary>The hosted-page gateway sends the customer straight back and then reports the order paid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentPlugin_ReturnsToTheShopAndReportsPaidAsync()
    {
        var plugin = new DemoPaymentPlugin("840", ["840", "978"], new DemoStoredCredentialState(new SystemClock(TimeProvider.System)));

        var session = await plugin.CreatePaymentAsync(
            new PaymentInstruction("INV9-1", 1000, "https://shop.example/return", null, null), CancellationToken.None);
        var status = await plugin.GetStatusAsync(session.GatewayOrderId, CancellationToken.None);

        session.RedirectUrl.Should().StartWith("https://shop.example/return?");
        session.GatewayOrderId.Should().Be("demo-INV9-1-1000");
        status.State.Should().Be(GatewayPaymentState.Paid);
        status.AmountMinor.Should().Be(1000);
    }

    /// <summary>The registrar succeeds for every module and answers the same reference for the same name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Registrar_RegistersDeterministicallyAndOffersAvailabilityBothWaysAsync()
    {
        var registrar = new DemoRegistrarProvider(new SystemClock(TimeProvider.System));
        var contact = new DomainContact(
            "Ani", "Hakobyan", null, "ani@example.test", "+374.10123456", "1 Main St", null, "Yerevan", "Yerevan", "0010", "AM");
        var order = new DomainRegistrationOrder("Example.COM", 1, true, true, null, null, contact);

        var first = await registrar.RegisterAsync(order, CancellationToken.None);
        var second = await registrar.RegisterAsync(order, CancellationToken.None);

        first.Success.Should().BeTrue();
        first.RegistrarRef.Should().Be("DEMO-example.com").And.Be(second.RegistrarRef);
        first.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        (await registrar.CheckAvailabilityAsync("free-name.com", CancellationToken.None)).Should().BeTrue();
        (await registrar.CheckAvailabilityAsync("taken-name.com", CancellationToken.None)).Should().BeFalse();
        (await registrar.GetDnsRecordsAsync(new DomainRef("example.com", "DEMO-example.com"), CancellationToken.None)).Should().HaveCount(3);
    }

    /// <summary>Provisioning succeeds, derives the reference from the account and returns usable credentials.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Provisioning_CreatesAnAccountAndReturnsCredentialsAsync()
    {
        var provider = new DemoProvisioningProvider();

        var result = await provider.ProvisionAsync(
            new ProvisionAccountOrder(1, "example.com", "user1", "secret", "basic"), CancellationToken.None);
        var credentials = await provider.GetCredentialsAsync(result.ProvisioningRef!, CancellationToken.None);
        var sso = await provider.GetCPanelSsoUrlAsync(result.ProvisioningRef!, "203.0.113.50", CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Created);
        result.ProvisioningRef.Should().Be("demo-user1@example.com");
        credentials.Username.Should().NotBeNullOrWhiteSpace();
        sso.Should().StartWith("https://");
    }

    /// <summary>
    /// The fixed rate table answers "1 unit = X base units", the rate the catalogue seeder stores, so
    /// a refresh leaves the seeded euro rate as it was.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExchangeRates_AreFixedAndConsistentAsync()
    {
        var source = new DemoExchangeRateSource();

        var result = await source.QuoteAsync(new ExchangeRateLookup("USD", ["AMD", "EUR", "XXX"]), CancellationToken.None);

        var rates = result.Value!.RatesToBase;
        rates["EUR"].Should().Be(Math.Round(1m / DemoCatalogueSeeder.EurosPerDollar, 8));
        rates["AMD"].Should().Be(Math.Round(1m / 385m, 8));
        rates.Should().NotContainKey("XXX");
    }
}
