using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the <see cref="ProductBundle"/> aggregate.</summary>
public sealed class ProductBundleTests
{
    /// <summary>Two components with the first as the primary one.</summary>
    /// <returns>A fresh list each call, so a test may not share state with another.</returns>
    private static List<ProductBundleItem> Items() =>
        [ProductBundleItem.Create(1, true), ProductBundleItem.Create(2, false)];

    /// <summary>A valid bundle with the given discount, domain and components.</summary>
    /// <param name="includesDomain">Whether the bundle includes a domain.</param>
    /// <param name="domainYears">The years of registration.</param>
    /// <param name="type">The discount type.</param>
    /// <param name="amount">The discount amount.</param>
    /// <param name="currency">The discount currency.</param>
    /// <param name="items">The components; two by default.</param>
    /// <returns>The created bundle.</returns>
    private static ProductBundle NewBundle(
        bool includesDomain = true,
        int? domainYears = 2,
        BundleDiscountType type = BundleDiscountType.Percentage,
        decimal amount = 15m,
        string? currency = null,
        List<ProductBundleItem>? items = null)
    {
        return ProductBundle.Create("Starter", "Hosting and a domain", 3, includesDomain, domainYears, type, amount, currency, items ?? Items());
    }

    /// <summary>Create keeps every field, activates the bundle and holds the components.</summary>
    [Fact]
    public void Create_SetsFieldsAndIsActive()
    {
        var bundle = NewBundle();

        bundle.Name.Should().Be("Starter");
        bundle.Description.Should().Be("Hosting and a domain");
        bundle.SortOrder.Should().Be(3);
        bundle.IsActive.Should().BeTrue();
        bundle.IncludesDomain.Should().BeTrue();
        bundle.DomainYears.Should().Be(2);
        bundle.DiscountType.Should().Be(BundleDiscountType.Percentage);
        bundle.DiscountAmount.Should().Be(15m);
        bundle.DiscountCurrency.Should().BeNull();
        bundle.Items.Should().HaveCount(2);
    }

    /// <summary>A blank name is refused.</summary>
    [Fact]
    public void Create_RefusesABlankName()
    {
        var act = () => ProductBundle.Create(" ", null, 0, false, null, BundleDiscountType.None, 0m, null, Items());

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A fixed discount stores its currency upper-cased.</summary>
    [Fact]
    public void Create_NormalisesAFixedDiscountCurrency()
    {
        var bundle = NewBundle(type: BundleDiscountType.Fixed, amount: 5m, currency: " eur ");

        bundle.DiscountCurrency.Should().Be("EUR");
    }

    /// <summary>A bundle needs at least one component.</summary>
    [Fact]
    public void Create_RefusesNoItems()
    {
        var act = () => NewBundle(items: []);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductBundleItemsRequired);
    }

    /// <summary>The same product cannot be listed twice.</summary>
    [Fact]
    public void Create_RefusesADuplicateProduct()
    {
        var act = () => NewBundle(items: [ProductBundleItem.Create(1, true), ProductBundleItem.Create(1, false)]);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductBundleItemDuplicate);
    }

    /// <summary>Zero or two primary components are refused.</summary>
    /// <param name="firstPrimary">Whether the first component is primary.</param>
    /// <param name="secondPrimary">Whether the second component is primary.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Create_RefusesAnythingButOnePrimary(bool firstPrimary, bool secondPrimary)
    {
        var act = () => NewBundle(items: [ProductBundleItem.Create(1, firstPrimary), ProductBundleItem.Create(2, secondPrimary)]);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductBundlePrimaryInvalid);
    }

    /// <summary>A domain needs 1 to 10 years and no domain takes none.</summary>
    /// <param name="includesDomain">Whether a domain is included.</param>
    /// <param name="years">The years given.</param>
    [Theory]
    [InlineData(true, null)]
    [InlineData(true, 0)]
    [InlineData(true, 11)]
    [InlineData(false, 1)]
    public void Create_RefusesAnIncoherentDomainTerm(bool includesDomain, int? years)
    {
        var act = () => NewBundle(includesDomain, years);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductBundleDomainTermInvalid);
    }

    /// <summary>The edges of the domain term are accepted.</summary>
    /// <param name="years">The years given.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Create_AcceptsTheDomainTermEdges(int years)
    {
        NewBundle(true, years).DomainYears.Should().Be(years);
    }

    /// <summary>A bundle without a domain keeps no years.</summary>
    [Fact]
    public void Create_WithoutADomain_HasNoYears()
    {
        NewBundle(false, null).DomainYears.Should().BeNull();
    }

    /// <summary>Every incoherent discount is refused, every coherent one accepted.</summary>
    /// <param name="type">The discount type.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="valid">Whether the discount must be accepted.</param>
    [Theory]
    [InlineData(BundleDiscountType.None, 0, null, true)]
    [InlineData(BundleDiscountType.None, 5, null, false)]
    [InlineData(BundleDiscountType.None, 0, "USD", false)]
    [InlineData(BundleDiscountType.Percentage, 0, null, true)]
    [InlineData(BundleDiscountType.Percentage, 100, null, true)]
    [InlineData(BundleDiscountType.Percentage, 100.01, null, false)]
    [InlineData(BundleDiscountType.Percentage, -1, null, false)]
    [InlineData(BundleDiscountType.Percentage, 10, "USD", false)]
    [InlineData(BundleDiscountType.Fixed, 0, "USD", true)]
    [InlineData(BundleDiscountType.Fixed, 250, "USD", true)]
    [InlineData(BundleDiscountType.Fixed, -1, "USD", false)]
    [InlineData(BundleDiscountType.Fixed, 5, null, false)]
    [InlineData(BundleDiscountType.Fixed, 5, "US", false)]
    public void Create_ChecksTheDiscount(BundleDiscountType type, double amount, string? currency, bool valid)
    {
        var act = () => NewBundle(type: type, amount: (decimal)amount, currency: currency);

        if (valid)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductBundleDiscountInvalid);
        }
    }

    /// <summary>Update replaces the fields, keeps the rows of products that stay and drops the others.</summary>
    [Fact]
    public void Update_ReplacesFieldsAndKeepsTheStayingItems()
    {
        var bundle = NewBundle();
        var kept = bundle.Items.Single(i => i.ProductId == 1);

        bundle.Update("Pro", null, 9, false, null, BundleDiscountType.None, 0m, null, [ProductBundleItem.Create(1, false), ProductBundleItem.Create(3, true)]);

        bundle.Name.Should().Be("Pro");
        bundle.Description.Should().BeNull();
        bundle.SortOrder.Should().Be(9);
        bundle.IncludesDomain.Should().BeFalse();
        bundle.Items.Select(i => i.ProductId).Should().BeEquivalentTo([1, 3]);
        bundle.Items.Single(i => i.ProductId == 1).Should().BeSameAs(kept);
        kept.IsPrimary.Should().BeFalse();
        bundle.Items.Single(i => i.ProductId == 3).IsPrimary.Should().BeTrue();
    }

    /// <summary>A refused update changes nothing.</summary>
    [Fact]
    public void Update_WhenRefused_LeavesTheBundleAsItWas()
    {
        var bundle = NewBundle();

        var act = () => bundle.Update("Other", null, 0, true, 99, BundleDiscountType.None, 0m, null, Items());

        act.Should().Throw<DomainRuleException>();
        bundle.Name.Should().Be("Starter");
        bundle.DomainYears.Should().Be(2);
        bundle.Items.Should().HaveCount(2);
    }

    /// <summary>Deactivate and Activate toggle the flag.</summary>
    [Fact]
    public void DeactivateAndActivate_ToggleIsActive()
    {
        var bundle = NewBundle();

        bundle.Deactivate();
        bundle.IsActive.Should().BeFalse();

        bundle.Activate();
        bundle.IsActive.Should().BeTrue();
    }
}
