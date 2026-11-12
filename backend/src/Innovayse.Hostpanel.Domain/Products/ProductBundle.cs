using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// Products sold together as one offer: a hosting plan with a domain, say, at a discount. An
/// aggregate root that owns its <see cref="ProductBundleItem"/> components.
/// Stored in the <c>ProductBundles</c> table.
/// </summary>
/// <remarks>
/// The domain is not a component: its price depends on the TLD the client picks, so the bundle
/// only records that a registration is included (<see cref="IncludesDomain"/>) and for how many
/// years (<see cref="DomainYears"/>), and the domain line is priced when the order is placed.
/// </remarks>
public sealed class ProductBundle : BaseEntity, IAggregateRoot
{
    /// <summary>Longest a bundle name may be: matches the <c>ProductBundles.Name</c> column.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Longest a description may be: matches the <c>ProductBundles.Description</c> column.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>The fewest years of domain registration a bundle may include.</summary>
    public const int MinDomainYears = 1;

    /// <summary>The most years of domain registration a bundle may include.</summary>
    public const int MaxDomainYears = 10;

    /// <summary>The largest percentage discount.</summary>
    public const decimal MaxDiscountPercentage = 100m;

    /// <summary>Internal mutable components list.</summary>
    private readonly List<ProductBundleItem> _items = [];

    /// <summary>Gets the display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description shown in admin and storefront.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets whether the bundle is visible and available for new orders.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the operator-defined position in the list, lowest first.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether the bundle includes a domain registration.</summary>
    public bool IncludesDomain { get; private set; }

    /// <summary>Gets the years of registration included, from <see cref="MinDomainYears"/> to <see cref="MaxDomainYears"/>; null when no domain is included.</summary>
    public int? DomainYears { get; private set; }

    /// <summary>Gets how the bundle is discounted.</summary>
    public BundleDiscountType DiscountType { get; private set; }

    /// <summary>Gets the discount: a percentage for <see cref="BundleDiscountType.Percentage"/>, an amount for <see cref="BundleDiscountType.Fixed"/>, zero for none.</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Gets the ISO 4217 code of a fixed discount; null for any other discount type.</summary>
    public string? DiscountCurrency { get; private set; }

    /// <summary>Gets the components of the bundle.</summary>
    public IReadOnlyList<ProductBundleItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProductBundle() { }

    /// <summary>Whether a domain registration period is acceptable.</summary>
    /// <param name="includesDomain">Whether the bundle includes a domain.</param>
    /// <param name="domainYears">The years, or null.</param>
    /// <returns>
    /// <see langword="true"/> when a bundle with a domain names a period from <see cref="MinDomainYears"/>
    /// to <see cref="MaxDomainYears"/>, and a bundle without one names none.
    /// </returns>
    public static bool IsValidDomainTerm(bool includesDomain, int? domainYears)
    {
        return includesDomain
            ? domainYears is >= MinDomainYears and <= MaxDomainYears
            : domainYears is null;
    }

    /// <summary>Whether a discount is coherent.</summary>
    /// <param name="type">The discount type.</param>
    /// <param name="amount">The discount amount.</param>
    /// <param name="currency">The currency of a fixed discount, or null.</param>
    /// <returns>
    /// <see langword="true"/> for no discount with a zero amount and no currency, a percentage from 0 to
    /// <see cref="MaxDiscountPercentage"/> with no currency, or a non-negative fixed amount with a currency code.
    /// </returns>
    public static bool IsValidDiscount(BundleDiscountType type, decimal amount, string? currency)
    {
        return type switch
        {
            BundleDiscountType.None => amount == 0m && currency is null,
            BundleDiscountType.Percentage => amount is >= 0m and <= MaxDiscountPercentage && currency is null,
            BundleDiscountType.Fixed => amount >= 0m && CurrencyCode.IsValid(currency?.Trim()),
            _ => false,
        };
    }

    /// <summary>Creates a new active bundle.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="sortOrder">Position in the list, lowest first.</param>
    /// <param name="includesDomain">Whether a domain registration is included.</param>
    /// <param name="domainYears">Years of registration included; null when no domain is included.</param>
    /// <param name="discountType">How the bundle is discounted.</param>
    /// <param name="discountAmount">The discount amount.</param>
    /// <param name="discountCurrency">The currency of a fixed discount; null otherwise.</param>
    /// <param name="items">The components: at least one, no product twice, exactly one primary.</param>
    /// <returns>A new active <see cref="ProductBundle"/>.</returns>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    /// <exception cref="DomainRuleException">The term, the discount or the components break a bundle rule.</exception>
    public static ProductBundle Create(
        string name,
        string? description,
        int sortOrder,
        bool includesDomain,
        int? domainYears,
        BundleDiscountType discountType,
        decimal discountAmount,
        string? discountCurrency,
        IReadOnlyCollection<ProductBundleItem> items)
    {
        var bundle = new ProductBundle { IsActive = true };
        bundle.Update(name, description, sortOrder, includesDomain, domainYears, discountType, discountAmount, discountCurrency, items);
        return bundle;
    }

    /// <summary>
    /// Replaces the bundle's details and components. Every rule is checked before anything changes,
    /// so a refused update leaves the bundle as it was.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="sortOrder">New position in the list.</param>
    /// <param name="includesDomain">Whether a domain registration is included.</param>
    /// <param name="domainYears">Years of registration included; null when no domain is included.</param>
    /// <param name="discountType">How the bundle is discounted.</param>
    /// <param name="discountAmount">The discount amount.</param>
    /// <param name="discountCurrency">The currency of a fixed discount; null otherwise.</param>
    /// <param name="items">The new components: at least one, no product twice, exactly one primary.</param>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    /// <exception cref="DomainRuleException">The term, the discount or the components break a bundle rule.</exception>
    public void Update(
        string name,
        string? description,
        int sortOrder,
        bool includesDomain,
        int? domainYears,
        BundleDiscountType discountType,
        decimal discountAmount,
        string? discountCurrency,
        IReadOnlyCollection<ProductBundleItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);

        var currency = discountCurrency?.Trim();
        if (!IsValidDomainTerm(includesDomain, domainYears))
        {
            throw new DomainRuleException(ProductsErrors.ProductBundleDomainTermInvalid, "A bundle with a domain needs 1 to 10 years; one without names no years.");
        }

        if (!IsValidDiscount(discountType, discountAmount, currency))
        {
            throw new DomainRuleException(ProductsErrors.ProductBundleDiscountInvalid, "The discount type, amount and currency do not agree.");
        }

        CheckItems(items);

        Name = name.Trim();
        Description = description;
        SortOrder = sortOrder;
        IncludesDomain = includesDomain;
        DomainYears = domainYears;
        DiscountType = discountType;
        DiscountAmount = discountAmount;
        DiscountCurrency = currency is null ? null : CurrencyCode.Normalise(currency);
        ApplyItems(items);
    }

    /// <summary>Marks the bundle inactive: hides it from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Marks the bundle active: makes it visible in the storefront.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Checks the components against the bundle rules.</summary>
    /// <param name="items">The candidate components.</param>
    /// <exception cref="DomainRuleException">There is none, a product repeats, or the primary count is not one.</exception>
    private static void CheckItems(IReadOnlyCollection<ProductBundleItem> items)
    {
        if (items.Count == 0)
        {
            throw new DomainRuleException(ProductsErrors.ProductBundleItemsRequired, "A bundle needs at least one product.");
        }

        if (items.Select(i => i.ProductId).Distinct().Count() != items.Count)
        {
            throw new DomainRuleException(ProductsErrors.ProductBundleItemDuplicate, "A product may appear in a bundle once.");
        }

        if (items.Count(i => i.IsPrimary) != 1)
        {
            throw new DomainRuleException(ProductsErrors.ProductBundlePrimaryInvalid, "A bundle needs exactly one primary product.");
        }
    }

    /// <summary>
    /// Makes the stored components match the given ones, keeping the rows of products that stay so the
    /// store updates them in place instead of deleting and re-inserting under the unique index.
    /// </summary>
    /// <param name="items">The components the bundle must end up with.</param>
    private void ApplyItems(IReadOnlyCollection<ProductBundleItem> items)
    {
        _items.RemoveAll(existing => items.All(i => i.ProductId != existing.ProductId));
        foreach (var item in items)
        {
            var existing = _items.Find(e => e.ProductId == item.ProductId);
            if (existing is null)
            {
                _items.Add(item);
            }
            else
            {
                existing.SetPrimary(item.IsPrimary);
            }
        }
    }
}
