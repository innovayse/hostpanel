namespace Innovayse.Hostpanel.Application.Products.Commands.ReorderProductOptionGroups;

/// <summary>Puts a product's option groups in a new order.</summary>
/// <param name="ProductId">
/// Product whose groups are reordered. A value type, so it is left non-nullable: a missing <c>int</c> in
/// the body binds to <c>0</c> and the validator refuses it.
/// </param>
/// <param name="GroupIds">Every one of the product's group ids, once each, in the order they should appear.</param>
public sealed record ReorderProductOptionGroupsCommand(int ProductId, IReadOnlyList<int> GroupIds);
