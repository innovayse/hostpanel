namespace Innovayse.Domain.Products;

using Innovayse.Domain.Common;

/// <summary>
/// Groups related products (e.g., "Shared Hosting", "VPS", "SSL Certificates").
/// An aggregate root — owns the list of products in the catalogue.
/// Stored in the <c>product_groups</c> table.
/// </summary>
public sealed class ProductGroup : AggregateRoot
{
    /// <summary>Internal mutable products list.</summary>
    private readonly List<Product> _products = [];

    /// <summary>Gets the group display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description shown in admin and storefront.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets whether this group is visible and available for new orders.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets all products belonging to this group.</summary>
    public IReadOnlyList<Product> Products => _products.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ProductGroup() : base(0) { }

    /// <summary>
    /// Creates a new active product group.
    /// </summary>
    /// <param name="name">Display name for the group.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>A new active <see cref="ProductGroup"/>.</returns>
    public static ProductGroup Create(string name, string? description)
    {
        var group = new ProductGroup { Name = name, Description = description, IsActive = true };
        return group;
    }

    /// <summary>
    /// Updates the group name and description.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>Marks the group inactive — hides it from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Marks the group active — makes it visible in the storefront.</summary>
    public void Activate() => IsActive = true;
}
