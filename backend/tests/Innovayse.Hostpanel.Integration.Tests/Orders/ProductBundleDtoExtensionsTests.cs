using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// The checks <see cref="ProductBundleDtoExtensions.ResolveOrderItems"/> makes of a bundle order's lines, without a
/// database: the bundle is active, the product lines are exactly its products, the domain line agrees with the
/// bundle's included domain, and the domain line takes the bundle's registration period.
/// </summary>
public sealed class ProductBundleDtoExtensionsTests
{
    /// <summary>The bundle's primary product.</summary>
    private const int Hosting = 11;

    /// <summary>The bundle's second product.</summary>
    private const int Mailbox = 12;

    /// <summary>A domain product, the one a domain line names.</summary>
    private const int DomainProduct = 99;

    /// <summary>The registration period the bundle includes.</summary>
    private const int BundleYears = 2;

    /// <summary>Builds a bundle of <see cref="Hosting"/> and <see cref="Mailbox"/>.</summary>
    /// <param name="isActive">Whether the bundle is active.</param>
    /// <param name="includesDomain">Whether it includes a domain registration.</param>
    /// <returns>The bundle.</returns>
    private static ProductBundleDto Bundle(bool isActive = true, bool includesDomain = true) => new(
        7, "Starter pack", null, isActive, 0, includesDomain, includesDomain ? BundleYears : null,
        BundleDiscountType.Percentage, 10m, null,
        [new ProductBundleComponentDto(Hosting, "Hosting", true), new ProductBundleComponentDto(Mailbox, "Mailbox", false)]);

    /// <summary>Builds a product line.</summary>
    /// <param name="productId">The product ordered.</param>
    /// <returns>The line.</returns>
    private static PlaceOrderItemDto Product(int productId) => new(productId, BillingCycle.Monthly, null, null);

    /// <summary>Builds a domain line.</summary>
    /// <param name="action">What is done with the domain.</param>
    /// <param name="years">The period the line sends.</param>
    /// <returns>The line.</returns>
    private static PlaceOrderItemDto DomainLine(DomainAction action = DomainAction.Register, int? years = 1) =>
        new(DomainProduct, BillingCycle.Annual, "example.com", null, action, "secret-code", years);

    /// <summary>The right products and a domain line pass, and the domain line's period becomes the bundle's.</summary>
    [Fact]
    public void ExactComponentsAndADomain_Pass_WithTheBundlesYears()
    {
        var result = Bundle().ResolveOrderItems([Product(Mailbox), Product(Hosting), DomainLine(years: 5)]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
        result.Value!.Single(i => i.DomainAction is not null).Years.Should().Be(BundleYears);
        result.Value!.Where(i => i.DomainAction is null).Select(i => i.Years).Should().OnlyContain(y => y == null);
    }

    /// <summary>A line with no period at all gets the bundle's as well, so the registry is asked for the right term.</summary>
    [Fact]
    public void ADomainLineWithoutYears_TakesTheBundlesYears()
    {
        var result = Bundle().ResolveOrderItems([Product(Hosting), Product(Mailbox), DomainLine(years: null)]);

        result.Value!.Single(i => i.DomainAction is not null).Years.Should().Be(BundleYears);
    }

    /// <summary>A transfer is as good as a registration.</summary>
    [Fact]
    public void ATransferLine_Passes()
    {
        var result = Bundle().ResolveOrderItems([Product(Hosting), Product(Mailbox), DomainLine(DomainAction.Transfer)]);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>A bundle without a domain passes with product lines alone.</summary>
    [Fact]
    public void ABundleWithoutADomain_PassesWithProductLinesOnly()
    {
        var result = Bundle(includesDomain: false).ResolveOrderItems([Product(Hosting), Product(Mailbox)]);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>An inactive bundle may not be ordered.</summary>
    [Fact]
    public void AnInactiveBundle_IsRefused()
    {
        var result = Bundle(isActive: false).ResolveOrderItems([Product(Hosting), Product(Mailbox), DomainLine()]);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_AVAILABLE);
    }

    /// <summary>A missing component, an extra product and a product sent twice are each refused.</summary>
    /// <param name="productIds">The product lines to send.</param>
    [Theory]
    [InlineData(Hosting)]
    [InlineData(Hosting, Mailbox, 13)]
    [InlineData(Hosting, Hosting, Mailbox)]
    [InlineData(Hosting, 13)]
    public void ProductLinesThatAreNotTheComponents_AreRefused(params int[] productIds)
    {
        var items = productIds.Select(Product).Append(DomainLine()).ToList();

        var result = Bundle().ResolveOrderItems(items);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_BUNDLE_ITEMS_MISMATCH);
    }

    /// <summary>A bundle that includes a domain refuses an order with no domain line.</summary>
    [Fact]
    public void ABundleWithADomain_RefusesAnOrderWithoutOne()
    {
        var result = Bundle().ResolveOrderItems([Product(Hosting), Product(Mailbox)]);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_MISMATCH);
    }

    /// <summary>A bundle without a domain refuses a domain line, and so does a bundle that gets two.</summary>
    /// <param name="includesDomain">Whether the bundle includes a domain.</param>
    /// <param name="domainLines">How many domain lines the order carries.</param>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void DomainLinesThatDoNotAgreeWithTheBundle_AreRefused(bool includesDomain, int domainLines)
    {
        var items = new List<PlaceOrderItemDto> { Product(Hosting), Product(Mailbox) };
        items.AddRange(Enumerable.Repeat(DomainLine(), domainLines));

        var result = Bundle(includesDomain: includesDomain).ResolveOrderItems(items);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_MISMATCH);
    }

    /// <summary>A renewal is not part of a bundle.</summary>
    [Fact]
    public void ARenewalLine_IsRefused()
    {
        var result = Bundle().ResolveOrderItems([Product(Hosting), Product(Mailbox), DomainLine(DomainAction.Renew)]);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_BUNDLE_DOMAIN_ACTION_NOT_ALLOWED);
    }
}
