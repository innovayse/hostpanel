namespace Innovayse.Application.Products.Queries.GetProducts;

using Innovayse.Domain.Products.Interfaces;

/// <summary>Returns a filtered list of products as DTOs.</summary>
public sealed class GetProductsHandler(IProductRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetProductsQuery"/>.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching product DTOs.</returns>
    public async Task<IReadOnlyList<ProductDto>> HandleAsync(GetProductsQuery qry, CancellationToken ct)
    {
        var products = await repo.ListAsync(qry.GroupId, qry.ActiveOnly, ct);
        return products
            .Select(p => new ProductDto(
                p.Id,
                p.GroupId,
                p.Name,
                p.Description,
                p.Website,
                p.Type,
                p.Status,
                new ProductPricingDto(p.MonthlyPrice, p.AnnualPrice)))
            .ToList();
    }
}
