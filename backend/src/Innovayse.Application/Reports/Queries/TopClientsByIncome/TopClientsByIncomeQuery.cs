namespace Innovayse.Application.Reports.Queries.TopClientsByIncome;

/// <summary>Query for the Top 10 Clients by Income (transaction-based) report.</summary>
public record TopClientsByIncomeQuery(int Take = 10);
