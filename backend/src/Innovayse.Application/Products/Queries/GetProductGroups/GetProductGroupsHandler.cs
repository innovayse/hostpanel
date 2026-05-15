namespace Innovayse.Application.Products.Queries.GetProductGroups;

using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns all product groups as DTOs.</summary>
public sealed class GetProductGroupsHandler(IProductGroupRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetProductGroupsQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product group DTOs.</returns>
    public async Task<IReadOnlyList<ProductGroupDto>> HandleAsync(GetProductGroupsQuery qry, CancellationToken ct)
    {
        var groups = await repo.ListAsync(ct);
        return groups
            .Where(g => !qry.ActiveOnly || g.IsActive)
            .Select(g => new ProductGroupDto(g.Id, g.Name, g.Description, g.IsActive, g.Products.Count))
            .ToList();
    }
}
