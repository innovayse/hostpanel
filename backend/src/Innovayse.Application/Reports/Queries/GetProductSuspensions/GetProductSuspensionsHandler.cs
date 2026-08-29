namespace Innovayse.Application.Reports.Queries.GetProductSuspensions;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="GetProductSuspensionsQuery"/>.</summary>
/// <param name="repo">Reporting data access.</param>
public sealed class GetProductSuspensionsHandler(IReportRepository repo)
{
    /// <summary>Returns every currently suspended service with its suspension details.</summary>
    /// <param name="query">The query. Carries no filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One row per suspended service.</returns>
    public Task<IReadOnlyList<ProductSuspensionRowDto>> HandleAsync(
        GetProductSuspensionsQuery query, CancellationToken ct)
        => repo.GetProductSuspensionsAsync(ct);
}
