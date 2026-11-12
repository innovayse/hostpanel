using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>Represents one configurable question of a product with its choices, in display order.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">Product the group configures.</param>
/// <param name="Name">The question the client sees.</param>
/// <param name="Type">How the client answers.</param>
/// <param name="IsRequired">Whether the client must answer before the order can be placed.</param>
/// <param name="DisplayOrder">Position within the product, ascending.</param>
/// <param name="MinQuantity">Least amount, for a quantity group; otherwise <see langword="null"/>.</param>
/// <param name="MaxQuantity">Most amount, for a quantity group; otherwise <see langword="null"/>.</param>
/// <param name="Options">The group's choices, ordered by their display order.</param>
public sealed record ProductOptionGroupDto(
    int Id,
    int ProductId,
    string Name,
    ProductOptionType Type,
    bool IsRequired,
    int DisplayOrder,
    int? MinQuantity,
    int? MaxQuantity,
    IReadOnlyList<ProductOptionDto> Options);
