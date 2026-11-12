using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>Represents one choice inside a product option group.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="Name">The name the client sees.</param>
/// <param name="PriceModifier">What the choice adds to the recurring price, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="PriceType">Whether the modifiers are an amount in the base currency or a percentage.</param>
/// <param name="SetupFeeModifier">What the choice adds to the one-off setup fee, in the unit named by <paramref name="PriceType"/>.</param>
/// <param name="DisplayOrder">Position within the group, ascending.</param>
public sealed record ProductOptionDto(
    int Id,
    string Name,
    decimal PriceModifier,
    ProductOptionPriceType PriceType,
    decimal SetupFeeModifier,
    int DisplayOrder);
