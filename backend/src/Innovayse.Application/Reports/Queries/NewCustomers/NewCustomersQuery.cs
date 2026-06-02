namespace Innovayse.Application.Reports.Queries.NewCustomers;

/// <summary>Query for the New Customers report.</summary>
/// <param name="Year">Year to report on (defaults to current year).</param>
public record NewCustomersQuery(int Year);
