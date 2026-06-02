namespace Innovayse.Application.Reports.Queries.NewCustomers;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="NewCustomersQuery"/>.</summary>
public sealed class NewCustomersHandler(IReportRepository repo)
{
    /// <summary>Returns monthly new customer and order metrics.</summary>
    public Task<IReadOnlyList<NewCustomerDto>> HandleAsync(NewCustomersQuery query, CancellationToken ct)
        => repo.GetNewCustomersAsync(query.Year, ct);
}
