using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// Groups related products (e.g., "Shared Hosting", "VPS", "SSL Certificates").
/// An aggregate root: owns the list of products in the catalogue.
/// Stored in the <c>ProductGroups</c> table.
/// </summary>
public sealed class ProductGroup : BaseEntity, IAggregateRoot
{
    /// <summary>Internal mutable products list.</summary>
    private readonly List<Product> _products = [];

    /// <summary>Gets the group display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description shown in admin and storefront.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets whether this group is visible and available for new orders.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Backing field for the payment methods this group may be paid with.</summary>
    private List<string> _allowedPaymentMethods = [];

    /// <summary>
    /// Gets the payment method module ids an order or invoice of this group may be paid with; empty means every
    /// method on offer is allowed, which is what a group has until an operator restricts it.
    /// </summary>
    public IReadOnlyList<string> AllowedPaymentMethods => _allowedPaymentMethods.AsReadOnly();

    /// <summary>Gets all products belonging to this group.</summary>
    public IReadOnlyList<Product> Products => _products.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProductGroup() { }

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

    /// <summary>
    /// Replaces the payment methods this group may be paid with. Blank entries are dropped and a method named
    /// twice (ignoring case) is kept once, in the order first given.
    /// </summary>
    /// <param name="methods">Payment method module ids; an empty list lifts the restriction.</param>
    public void SetAllowedPaymentMethods(IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        _allowedPaymentMethods = methods
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Works out which payment methods every one of the given groups allows at once: the intersection of the
    /// restricted groups' sets. A group with no restriction allows everything and so takes no part.
    /// </summary>
    /// <param name="groups">The groups an order or invoice draws its products from.</param>
    /// <returns>
    /// <see langword="null"/> when no group is restricted (every method on offer may be used); otherwise the methods all
    /// restricted groups share, which is empty when they share none.
    /// </returns>
    public static IReadOnlyList<string>? CommonAllowedPaymentMethods(IEnumerable<ProductGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        IReadOnlyList<string>? common = null;
        foreach (var group in groups.Where(g => g._allowedPaymentMethods.Count > 0))
        {
            common = common is null
                ? group._allowedPaymentMethods
                : common.Intersect(group._allowedPaymentMethods, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return common;
    }

    /// <summary>Marks the group inactive: hides it from the storefront.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Marks the group active: makes it visible in the storefront.</summary>
    public void Activate() => IsActive = true;
}
