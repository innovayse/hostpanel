using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for <see cref="RegisteredDomain.ImportFromRegistrar"/>, the factory behind importing a domain a registrar account already holds.</summary>
public sealed class RegisteredDomainRegistrarImportTests
{
    /// <summary>The ISO 4217 code a test domain is priced in.</summary>
    private const string Currency = "USD";

    /// <summary>The fixed moment of the import.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A domain that has not expired is stored active with the registrar's reference and expiry, its due date following the expiry and its price the one given.</summary>
    [Fact]
    public void ImportFromRegistrar_ARunningDomain_IsActiveWithTheRegistrarsReferenceAndExpiry()
    {
        var expiresAt = Now.AddMonths(7);

        var domain = RegisteredDomain.ImportFromRegistrar(4, " Example.COM ", "registrar-one", "REF-9", expiresAt, Now, Currency, 12.5m);

        domain.Status.Should().Be(DomainStatus.Active);
        domain.ClientId.Should().Be(4);
        domain.Name.Should().Be("example.com");
        domain.Tld.Should().Be("com");
        domain.RegistrarModule.Should().Be("registrar-one");
        domain.RegistrarRef.Should().Be("REF-9");
        domain.ExpiresAt.Should().Be(expiresAt);
        domain.NextDueDate.Should().Be(expiresAt);
        domain.RegisteredAt.Should().Be(Now);
        domain.RecurringAmount.Should().Be(12.5m);
        domain.PriceCurrency.Should().Be(Currency);
    }

    /// <summary>A domain whose expiry has passed, or is the moment of the import, is stored expired.</summary>
    /// <param name="daysFromNow">Where the expiry lies from the import moment, in days.</param>
    [Theory]
    [InlineData(-30)]
    [InlineData(0)]
    public void ImportFromRegistrar_AnExpiredDomain_IsExpired(int daysFromNow)
    {
        var domain = RegisteredDomain.ImportFromRegistrar(1, "old.com", "registrar-one", "REF-1", Now.AddDays(daysFromNow), Now, Currency, 0m);

        domain.Status.Should().Be(DomainStatus.Expired);
    }

    /// <summary>The import is not a sale: no first payment, no order, no panel renewal, no event, so nobody is notified or invoiced.</summary>
    [Fact]
    public void ImportFromRegistrar_IsNotASale_NoPaymentOrderRenewalOrEvent()
    {
        var domain = RegisteredDomain.ImportFromRegistrar(1, "example.com", "registrar-one", "REF-1", Now.AddYears(1), Now, Currency, 9m);

        domain.FirstPaymentAmount.Should().Be(0m);
        domain.OrderId.Should().BeNull();
        domain.AutoRenew.Should().BeFalse();
        domain.DomainEvents.Should().BeEmpty();
    }

    /// <summary>A blank module or reference is refused, because the domain could never be addressed at its registrar.</summary>
    /// <param name="module">The registrar module.</param>
    /// <param name="registrarRef">The registrar's reference.</param>
    [Theory]
    [InlineData("", "REF-1")]
    [InlineData("  ", "REF-1")]
    [InlineData("registrar-one", "")]
    [InlineData("registrar-one", " ")]
    public void ImportFromRegistrar_ABlankModuleOrReference_IsRefused(string module, string registrarRef)
    {
        var import = () => RegisteredDomain.ImportFromRegistrar(1, "example.com", module, registrarRef, Now.AddYears(1), Now, Currency, 0m);

        import.Should().Throw<ArgumentException>();
    }
}
