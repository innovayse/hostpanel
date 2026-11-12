using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// One configurable question a client answers when ordering a product, such as "Operating system"
/// or "Extra IP addresses", with the <see cref="ProductOption"/> choices that answer it.
/// Belongs to a <see cref="Product"/>. Stored in the <c>ProductOptionGroups</c> table.
/// </summary>
/// <remarks>
/// <para>
/// The group is the aggregate root: its options are added, changed and removed only through it, so
/// what the <see cref="Type"/> allows is judged in one place. The type is fixed when the group is
/// created, because turning a list into a text field would leave the options it already holds
/// meaningless; to change the type, delete the group and create another.
/// </para>
/// <list type="bullet">
/// <item><see cref="ProductOptionType.Dropdown"/> and <see cref="ProductOptionType.Radio"/> hold any number of options.</item>
/// <item><see cref="ProductOptionType.Checkbox"/>, <see cref="ProductOptionType.Quantity"/> and <see cref="ProductOptionType.Text"/> hold at most one option.</item>
/// <item>A <see cref="ProductOptionType.Text"/> option carries no price: both modifiers must be zero.</item>
/// <item>A <see cref="ProductOptionType.Quantity"/> group carries a <see cref="MinQuantity"/> and a <see cref="MaxQuantity"/>, no other type does.</item>
/// </list>
/// <para>
/// A group with no option yet is allowed while an admin is still building it, so "exactly one"
/// for a single-option type and "at least one" for a list are not write-time rules. A group is
/// offered to a client only once it holds an option; the orderable query leaves the empty ones out.
/// </para>
/// </remarks>
public sealed class ProductOptionGroup : BaseEntity, IAggregateRoot
{
    /// <summary>The longest group name, matching the <c>ProductOptionGroups.Name</c> column.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Backing list for the group's options.</summary>
    private readonly List<ProductOption> _options = [];

    /// <summary>Gets the FK to the <see cref="Product"/> this group configures.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the question the client sees, such as "Operating system".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets how the client answers: pick from a list, switch on, enter an amount or type text.</summary>
    public ProductOptionType Type { get; private set; }

    /// <summary>Gets whether the client must answer before the order can be placed.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Gets the position of this group within the product, ascending.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Gets the least amount a client may enter; set for a <see cref="ProductOptionType.Quantity"/> group only.</summary>
    public int? MinQuantity { get; private set; }

    /// <summary>Gets the most a client may enter; set for a <see cref="ProductOptionType.Quantity"/> group only.</summary>
    public int? MaxQuantity { get; private set; }

    /// <summary>Gets the group's options in no particular order; sort by <see cref="ProductOption.DisplayOrder"/>.</summary>
    public IReadOnlyList<ProductOption> Options => _options.AsReadOnly();

    /// <summary>Whether the type takes one option only: a switch, an amount or a text value.</summary>
    private bool HoldsSingleOption => Type != ProductOptionType.Dropdown && Type != ProductOptionType.Radio;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ProductOptionGroup() { }

    /// <summary>Creates a group with no option yet.</summary>
    /// <param name="productId">FK to the product being configured.</param>
    /// <param name="name">The question the client sees.</param>
    /// <param name="type">How the client answers; fixed for the life of the group.</param>
    /// <param name="isRequired">Whether the client must answer.</param>
    /// <param name="displayOrder">Position within the product, ascending.</param>
    /// <param name="minQuantity">Least amount, required for <see cref="ProductOptionType.Quantity"/> and absent otherwise.</param>
    /// <param name="maxQuantity">Most amount, required for <see cref="ProductOptionType.Quantity"/> and absent otherwise.</param>
    /// <returns>A new <see cref="ProductOptionGroup"/>.</returns>
    /// <exception cref="ArgumentException">The name is blank or too long, or the type is not a defined one.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">The quantity range does not fit the type.</exception>
    public static ProductOptionGroup Create(
        int productId,
        string name,
        ProductOptionType type,
        bool isRequired,
        int displayOrder,
        int? minQuantity,
        int? maxQuantity)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException($"{type} is not a defined option type.", nameof(type));
        }

        var group = new ProductOptionGroup
        {
            ProductId = productId,
            Type = type,
        };
        group.Apply(name, isRequired, displayOrder, minQuantity, maxQuantity);
        return group;
    }

    /// <summary>Updates the group in place; its type stays as it was created.</summary>
    /// <param name="name">The question the client sees.</param>
    /// <param name="isRequired">Whether the client must answer.</param>
    /// <param name="displayOrder">Position within the product, ascending.</param>
    /// <param name="minQuantity">Least amount, required for <see cref="ProductOptionType.Quantity"/> and absent otherwise.</param>
    /// <param name="maxQuantity">Most amount, required for <see cref="ProductOptionType.Quantity"/> and absent otherwise.</param>
    /// <exception cref="ArgumentException">The name is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">The quantity range does not fit the type.</exception>
    public void Update(string name, bool isRequired, int displayOrder, int? minQuantity, int? maxQuantity)
    {
        Apply(name, isRequired, displayOrder, minQuantity, maxQuantity);
    }

    /// <summary>Moves the group to a position within its product.</summary>
    /// <param name="displayOrder">The new position, ascending.</param>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    public void Reorder(int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);

        DisplayOrder = displayOrder;
    }

    /// <summary>Adds a choice to the group.</summary>
    /// <param name="name">The name the client sees.</param>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="priceType">Whether the modifiers are money or a percentage.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    /// <param name="displayOrder">Position within the group, ascending.</param>
    /// <returns>The new option, which has no id until the group is saved.</returns>
    /// <exception cref="ArgumentException">The name is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">
    /// The type holds at most one option and one exists, a modifier is negative or a percentage above
    /// 100, or a text option carries a price.
    /// </exception>
    public ProductOption AddOption(
        string name,
        decimal priceModifier,
        ProductOptionPriceType priceType,
        decimal setupFeeModifier,
        int displayOrder)
    {
        if (HoldsSingleOption && _options.Count > 0)
        {
            throw new DomainRuleException(
                ProductsErrors.ProductOptionCountNotAllowed,
                $"A {Type} group holds one option and already has it.");
        }

        EnsureTypeAllowsPrice(priceModifier, setupFeeModifier);

        var option = new ProductOption(name, priceModifier, priceType, setupFeeModifier, displayOrder);
        _options.Add(option);
        return option;
    }

    /// <summary>Replaces every editable field of one of the group's options.</summary>
    /// <param name="optionId">The option to change.</param>
    /// <param name="name">The name the client sees.</param>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="priceType">Whether the modifiers are money or a percentage.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    /// <param name="displayOrder">Position within the group, ascending.</param>
    /// <exception cref="ArgumentException">The name is blank or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The display order is negative.</exception>
    /// <exception cref="DomainRuleException">
    /// The group has no such option, a modifier is negative or a percentage above 100, or a text
    /// option carries a price.
    /// </exception>
    public void UpdateOption(
        int optionId,
        string name,
        decimal priceModifier,
        ProductOptionPriceType priceType,
        decimal setupFeeModifier,
        int displayOrder)
    {
        var option = FindOption(optionId);
        EnsureTypeAllowsPrice(priceModifier, setupFeeModifier);

        option.Update(name, priceModifier, priceType, setupFeeModifier, displayOrder);
    }

    /// <summary>Removes one of the group's options.</summary>
    /// <param name="optionId">The option to remove.</param>
    /// <exception cref="DomainRuleException">The group has no such option.</exception>
    public void RemoveOption(int optionId)
    {
        _options.Remove(FindOption(optionId));
    }

    /// <summary>Checks the fields shared by creation and update, then writes them.</summary>
    /// <param name="name">The question the client sees.</param>
    /// <param name="isRequired">Whether the client must answer.</param>
    /// <param name="displayOrder">Position within the product, ascending.</param>
    /// <param name="minQuantity">Least amount, for a quantity group.</param>
    /// <param name="maxQuantity">Most amount, for a quantity group.</param>
    private void Apply(string name, bool isRequired, int displayOrder, int? minQuantity, int? maxQuantity)
    {
        var trimmed = ProductOptionRules.NormaliseName(name, NameMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        EnsureQuantityRange(minQuantity, maxQuantity);

        Name = trimmed;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;
    }

    /// <summary>
    /// A quantity group needs a range from zero or more up to at least one and at least the minimum;
    /// every other type must not carry one.
    /// </summary>
    /// <param name="minQuantity">Least amount.</param>
    /// <param name="maxQuantity">Most amount.</param>
    private void EnsureQuantityRange(int? minQuantity, int? maxQuantity)
    {
        var valid = Type == ProductOptionType.Quantity
            ? minQuantity is >= 0 && maxQuantity is >= 1 && maxQuantity >= minQuantity
            : minQuantity is null && maxQuantity is null;

        if (!valid)
        {
            throw new DomainRuleException(
                ProductsErrors.ProductOptionQuantityRangeInvalid,
                $"A {Type} group cannot have the range {minQuantity} to {maxQuantity}.");
        }
    }

    /// <summary>A text option is a free value and carries no price.</summary>
    /// <param name="priceModifier">What the choice adds to the recurring price.</param>
    /// <param name="setupFeeModifier">What the choice adds to the setup fee.</param>
    private void EnsureTypeAllowsPrice(decimal priceModifier, decimal setupFeeModifier)
    {
        if (Type == ProductOptionType.Text && (priceModifier != 0m || setupFeeModifier != 0m))
        {
            throw new DomainRuleException(
                ProductsErrors.ProductOptionPriceNotAllowed,
                "A text option carries no price.");
        }
    }

    /// <summary>Finds one of the group's options.</summary>
    /// <param name="optionId">The option to find.</param>
    /// <returns>The option.</returns>
    /// <exception cref="DomainRuleException">The group has no such option.</exception>
    private ProductOption FindOption(int optionId)
    {
        return _options.FirstOrDefault(o => o.Id == optionId)
            ?? throw new DomainRuleException(
                ProductsErrors.ProductOptionNotFound,
                $"The group has no option {optionId}.");
    }
}
