namespace Innovayse.Application.Admin.Queries.GetServiceUsageReport;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="GetServiceUsageReportQuery"/> by counting client services grouped by product name.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository for resolving product names.</param>
public sealed class GetServiceUsageReportHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo)
{
    /// <summary>
    /// Returns service usage counts grouped by product name, ordered by count descending.
    /// </summary>
    /// <param name="query">The service usage report query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product usage items ordered by count descending.</returns>
    public async Task<IReadOnlyList<ServiceUsageReportItemDto>> HandleAsync(
        GetServiceUsageReportQuery query, CancellationToken ct)
    {
        var services = await serviceRepo.GetAllAsync(ct);

        var grouped = services
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToList();

        var productIds = grouped.Select(g => g.ProductId);
        var products = await productRepo.FindByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id, p => p.Name);

        return grouped
            .Select(g => new ServiceUsageReportItemDto(
                productMap.TryGetValue(g.ProductId, out var name) ? name : $"Product #{g.ProductId}",
                g.Count))
            .OrderByDescending(r => r.Count)
            .ToList();
    }
}
