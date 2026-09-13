namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;
using Innovayse.Domain.Products.Events;

/// <summary>
/// A product in the catalogue that clients can order as a service.
/// Belongs to a <see cref="ProductGroup"/>.
/// Stored in the <c>products</c> table.
/// </summary>
public sealed class Product : AggregateRoot
{
    /// <summary>Gets the FK to the <see cref="ProductGroup"/> this product belongs to.</summary>
    public int GroupId { get; private set; }

    /// <summary>Gets the product display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the optional website URL for this product's landing page.</summary>
    public string? Website { get; private set; }

    /// <summary>Gets the optional URL-friendly slug for this product.</summary>
    public string? Slug { get; private set; }

    /// <summary>Gets the optional hosting package name used for provisioning (e.g. "starter", "pro").</summary>
    public string? PackageName { get; private set; }

    /// <summary>Gets the product type (hosting, VPS, domain, etc.).</summary>
    public ProductType Type { get; private set; }

    /// <summary>Gets the current status.</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>Gets the monthly price.</summary>
    public decimal MonthlyPrice { get; private set; }

    /// <summary>Gets the annual price.</summary>
    public decimal AnnualPrice { get; private set; }

    /// <summary>Gets the optional FK to the server group used for provisioning.</summary>
    public int? ServerGroupId { get; private set; }

    /// <summary>Gets the UTC timestamp when the product was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Backing list for per-currency prices.</summary>
    private readonly List<ProductPrice> _prices = [];

    /// <summary>Gets the stored prices, one per currency and cycle.</summary>
    /// <remarks>
    /// <see cref="MonthlyPrice"/> and <see cref="AnnualPrice"/> are the pre-multi-currency
    /// columns. They are kept for one release so a rollback has data to read, and are no longer
    /// written by anything; the migration copied them into USD rows here.
    /// </remarks>
    public IReadOnlyList<ProductPrice> Prices => _prices.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Product() : base(0) { }

    /// <summary>
    /// Creates a new active product and raises <see cref="ProductCreatedEvent"/>.
    /// </summary>
    /// <param name="groupId">FK to the parent product group.</param>
    /// <param name="name">Product display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="website">Optional website URL for the product's landing page.</param>
    /// <param name="slug">Optional URL-friendly slug for the product.</param>
    /// <param name="packageName">Optional hosting package name used for provisioning.</param>
    /// <param name="type">Product type.</param>
    /// <param name="monthlyPrice">Monthly price (≥ 0).</param>
    /// <param name="annualPrice">Annual price (≥ 0).</param>
    /// <param name="serverGroupId">Optional FK to the server group for provisioning.</param>
    /// <returns>A new active <see cref="Product"/>.</returns>
    public static Product Create(
        int groupId,
        string name,
        string? description,
        string? website,
        string? slug,
        string? packageName,
        ProductType type,
        decimal monthlyPrice,
        decimal annualPrice,
        int? serverGroupId = null)
    {
        var product = new Product
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            Website = website,
            Slug = slug,
            PackageName = packageName,
            Type = type,
            Status = ProductStatus.Active,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            ServerGroupId = serverGroupId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        product.AddDomainEvent(new ProductCreatedEvent(0, name, groupId));
        return product;
    }

    /// <summary>
    /// Updates the product name, description, website, prices, and server group.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="website">New website URL, or null to clear.</param>
    /// <param name="slug">New URL-friendly slug, or null to clear.</param>
    /// <param name="packageName">New hosting package name, or null to clear.</param>
    /// <param name="monthlyPrice">New monthly price.</param>
    /// <param name="annualPrice">New annual price.</param>
    /// <param name="serverGroupId">Optional FK to the server group, or null to clear.</param>
    public void Update(string name, string? description, string? website, string? slug, string? packageName, decimal monthlyPrice, decimal annualPrice, int? serverGroupId)
    {
        Name = name;
        Description = description;
        Website = website;
        Slug = slug;
        PackageName = packageName;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        ServerGroupId = serverGroupId;
    }

    /// <summary>Marks the product inactive so it cannot be ordered.</summary>
    public void Deactivate() => Status = ProductStatus.Inactive;

    /// <summary>Marks the product active so it can be ordered again.</summary>
    public void Activate() => Status = ProductStatus.Active;

    /// <summary>Sets (or replaces) the price for a currency and cycle.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <param name="amount">The amount; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void SetPrice(string currencyCode, BillingCycle cycle, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "A price cannot be negative.");

        var code = currencyCode.ToUpperInvariant();
        var existing = _prices.FirstOrDefault(p => p.CurrencyCode == code && p.Cycle == cycle);
        if (existing is null)
        {
            _prices.Add(new ProductPrice(code, cycle, amount));
        }
        else
        {
            existing.SetAmount(amount);
        }
    }

    /// <summary>Removes every price in a currency, so the product no longer sells in it.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    public void RemovePrices(string currencyCode)
    {
        var code = currencyCode.ToUpperInvariant();
        _prices.RemoveAll(p => p.CurrencyCode == code);
    }

    /// <summary>Reads the stored price for a currency and cycle.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <returns>The amount, or <see langword="null"/> when the product has no price for that pair.</returns>
    public decimal? PriceFor(string currencyCode, BillingCycle cycle)
    {
        var code = currencyCode.ToUpperInvariant();
        return _prices.FirstOrDefault(p => p.CurrencyCode == code && p.Cycle == cycle)?.Amount;
    }

    /// <summary>Whether the product has at least one price in a currency.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <returns><see langword="true"/> when a client billed in that currency can buy it.</returns>
    public bool SellsIn(string currencyCode)
    {
        var code = currencyCode.ToUpperInvariant();
        return _prices.Any(p => p.CurrencyCode == code);
    }
}
