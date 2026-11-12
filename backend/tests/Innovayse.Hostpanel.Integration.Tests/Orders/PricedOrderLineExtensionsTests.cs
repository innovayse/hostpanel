using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>How <see cref="PricedOrderLineExtensions.Describe"/> words an order line for the invoice, without a database.</summary>
public sealed class PricedOrderLineExtensionsTests
{
    /// <summary>An ordinary product line is described by the product's name alone.</summary>
    [Fact]
    public void ProductLine_IsTheProductName()
    {
        Line("Hosting", domain: null).Describe(null).Should().Be("Hosting");
    }

    /// <summary>A domain line names the domain, and a bundle order ends with the bundle's name.</summary>
    [Fact]
    public void DomainLineOfABundle_NamesTheDomainThenTheBundle()
    {
        Line("Domain Registration", "example.com").Describe("Starter").Should().Be("Domain Registration (example.com) (Starter)");
    }

    /// <summary>
    /// A product name, a domain and a bundle name at their longest together fit the invoice column: the product and
    /// domain part is cut and the bundle's name stays whole at the end.
    /// </summary>
    [Fact]
    public void LongestProductDomainAndBundle_FitTheColumnAndKeepTheBundleName()
    {
        var bundleName = new string('b', ProductBundle.NameMaxLength);

        var description = Line(new string('p', 200), new string('d', 253)).Describe(bundleName);

        description.Length.Should().Be(InvoiceItem.DescriptionMaxLength);
        description.Should().EndWith($" ({bundleName})");
    }

    /// <summary>Builds a priced line for a product, and for a registration of <paramref name="domain"/> when one is given.</summary>
    /// <param name="productName">The product's name.</param>
    /// <param name="domain">The domain name of a domain line, or <see langword="null"/> for a product line.</param>
    /// <returns>The priced line.</returns>
    private static PricedOrderLine Line(string productName, string? domain)
    {
        var item = new PlaceOrderItemDto(
            1, BillingCycle.Monthly, domain, null, domain is null ? null : DomainAction.Register);
        return new PricedOrderLine(item, productName, 10m, null);
    }
}
