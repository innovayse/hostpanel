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

    /// <summary>Gets the product type (hosting, VPS, domain, etc.).</summary>
    public ProductType Type { get; private set; }

    /// <summary>Gets the current status.</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>Gets the monthly price.</summary>
    public decimal MonthlyPrice { get; private set; }

    /// <summary>Gets the annual price.</summary>
    public decimal AnnualPrice { get; private set; }

    /// <summary>Gets the UTC timestamp when the product was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Product() : base(0) { }

    /// <summary>
    /// Creates a new active product and raises <see cref="ProductCreatedEvent"/>.
    /// </summary>
    /// <param name="groupId">FK to the parent product group.</param>
    /// <param name="name">Product display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="website">Optional website URL for the product's landing page.</param>
    /// <param name="type">Product type.</param>
    /// <param name="monthlyPrice">Monthly price (≥ 0).</param>
    /// <param name="annualPrice">Annual price (≥ 0).</param>
    /// <returns>A new active <see cref="Product"/>.</returns>
    public static Product Create(
        int groupId,
        string name,
        string? description,
        string? website,
        ProductType type,
        decimal monthlyPrice,
        decimal annualPrice)
    {
        var product = new Product
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            Website = website,
            Type = type,
            Status = ProductStatus.Active,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        product.AddDomainEvent(new ProductCreatedEvent(0, name, groupId));
        return product;
    }

    /// <summary>
    /// Updates the product name, description, website, and prices.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="website">New website URL, or null to clear.</param>
    /// <param name="monthlyPrice">New monthly price.</param>
    /// <param name="annualPrice">New annual price.</param>
    public void Update(string name, string? description, string? website, decimal monthlyPrice, decimal annualPrice)
    {
        Name = name;
        Description = description;
        Website = website;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
    }

    /// <summary>Marks the product inactive so it cannot be ordered.</summary>
    public void Deactivate() => Status = ProductStatus.Inactive;

    /// <summary>Marks the product active so it can be ordered again.</summary>
    public void Activate() => Status = ProductStatus.Active;
}
