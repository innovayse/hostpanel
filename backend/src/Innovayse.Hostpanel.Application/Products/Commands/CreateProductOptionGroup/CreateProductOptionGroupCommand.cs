using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductOptionGroup;

/// <summary>Adds an empty configurable option group to a product.</summary>
/// <param name="ProductId">Product the group configures.</param>
/// <param name="Name">The question the client sees.</param>
/// <param name="Type">How the client answers; fixed once the group exists.</param>
/// <param name="IsRequired">Whether the client must answer before the order can be placed.</param>
/// <param name="DisplayOrder">Position within the product, ascending.</param>
/// <param name="MinQuantity">Least amount; required for a quantity group and absent for every other type.</param>
/// <param name="MaxQuantity">Most amount; required for a quantity group and absent for every other type.</param>
public sealed record CreateProductOptionGroupCommand(
    int ProductId,
    string Name,
    ProductOptionType Type,
    bool IsRequired,
    int DisplayOrder,
    int? MinQuantity,
    int? MaxQuantity);
