using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOption;

/// <summary>Updates one choice of an option group.</summary>
/// <param name="Id">
/// Option primary key; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c>, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">The name the client sees.</param>
/// <param name="PriceModifier">What the choice adds to the recurring price, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="PriceType">Whether the modifiers are an amount in the base currency or a percentage of the product's price.</param>
/// <param name="SetupFeeModifier">What the choice adds to the one-off setup fee, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="DisplayOrder">Position within the group, ascending.</param>
public sealed record UpdateProductOptionCommand(
    int Id,
    string Name,
    decimal PriceModifier,
    ProductOptionPriceType PriceType,
    decimal SetupFeeModifier,
    int DisplayOrder);
