namespace Innovayse.Application.Products.Queries.GetProductGroups;

/// <summary>Returns all product groups.</summary>
/// <param name="ActiveOnly">When <see langword="true"/>, returns only active groups.</param>
public record GetProductGroupsQuery(bool ActiveOnly = false);
