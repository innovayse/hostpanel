namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOptionGroup;

/// <summary>Updates one option group; its type cannot be changed.</summary>
/// <param name="Id">
/// Option group primary key; wins over any id in the body. A value type, so it is left non-nullable:
/// a missing <c>int</c> in the body binds to <c>0</c>, and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">The question the client sees.</param>
/// <param name="IsRequired">Whether the client must answer before the order can be placed.</param>
/// <param name="DisplayOrder">Position within the product, ascending.</param>
/// <param name="MinQuantity">Least amount; required for a quantity group and absent for every other type.</param>
/// <param name="MaxQuantity">Most amount; required for a quantity group and absent for every other type.</param>
public sealed record UpdateProductOptionGroupCommand(
    int Id,
    string Name,
    bool IsRequired,
    int DisplayOrder,
    int? MinQuantity,
    int? MaxQuantity);
