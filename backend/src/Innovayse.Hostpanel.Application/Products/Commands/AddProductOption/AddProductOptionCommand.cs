using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Commands.AddProductOption;

/// <summary>Adds one choice to an option group.</summary>
/// <param name="GroupId">Option group the choice belongs to.</param>
/// <param name="Name">The name the client sees.</param>
/// <param name="PriceModifier">What the choice adds to the recurring price, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="PriceType">Whether the modifiers are an amount in the base currency or a percentage of the product's price.</param>
/// <param name="SetupFeeModifier">What the choice adds to the one-off setup fee, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="DisplayOrder">Position within the group, ascending.</param>
public sealed record AddProductOptionCommand(
    int GroupId,
    string Name,
    decimal PriceModifier,
    ProductOptionPriceType PriceType,
    decimal SetupFeeModifier,
    int DisplayOrder);
