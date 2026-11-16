using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Vat.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Vat;

/// <summary>
/// The VAT number port as the application registers it: the EU register needs no set-up, so every member state can be
/// checked, while the United Kingdom's can only be asked once the application's credentials for its authority are set.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class VatNumberCheckerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Whether the registered port can check numbers of a country.</summary>
    /// <param name="country">The country code.</param>
    /// <param name="credentials">Whether the UK authority credentials are set.</param>
    /// <returns>The port's answer.</returns>
    private bool IsConfiguredFor(string country, bool credentials)
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{HmrcVatOptions.SectionName}:{nameof(HmrcVatOptions.ClientId)}", credentials ? "app-id" : string.Empty);
            builder.UseSetting($"{HmrcVatOptions.SectionName}:{nameof(HmrcVatOptions.ClientSecret)}", credentials ? "app-secret" : string.Empty);
        });
        using var scope = host.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IVatNumberChecker>().IsConfiguredFor(country);
    }

    /// <summary>A member state of the EU can be checked with no credentials at all.</summary>
    [Fact]
    public void IsConfiguredFor_EuCountry_IsTrueWithoutCredentials()
    {
        IsConfiguredFor("DE", credentials: false).Should().BeTrue();
    }

    /// <summary>The United Kingdom cannot be checked while the authority credentials are not set.</summary>
    [Fact]
    public void IsConfiguredFor_UnitedKingdomWithoutCredentials_IsFalse()
    {
        IsConfiguredFor(VatRegisters.UnitedKingdom, credentials: false).Should().BeFalse();
    }

    /// <summary>The United Kingdom can be checked once the authority credentials are set.</summary>
    [Fact]
    public void IsConfiguredFor_UnitedKingdomWithCredentials_IsTrue()
    {
        IsConfiguredFor(VatRegisters.UnitedKingdom, credentials: true).Should().BeTrue();
    }
}
