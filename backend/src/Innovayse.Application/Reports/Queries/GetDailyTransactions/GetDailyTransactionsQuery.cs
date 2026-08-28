namespace Innovayse.Application.Reports.Queries.GetDailyTransactions;

/// <summary>Query for the daily transaction aggregates of one month.</summary>
/// <param name="Year">Calendar year, or null for the current UTC year.</param>
/// <param name="Month">Calendar month 1-12, or null for the current UTC month.</param>
public record GetDailyTransactionsQuery(int? Year = null, int? Month = null);
