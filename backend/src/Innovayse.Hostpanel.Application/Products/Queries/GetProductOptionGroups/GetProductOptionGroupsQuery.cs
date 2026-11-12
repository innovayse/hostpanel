namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductOptionGroups;

/// <summary>Requests every option group of one product with its options, as the admin edits them.</summary>
/// <param name="ProductId">Product whose groups are read.</param>
public sealed record GetProductOptionGroupsQuery(int ProductId);
