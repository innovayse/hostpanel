namespace Innovayse.Application.Reports.Common;

/// <summary>One line in a client statement.</summary>
public record ClientStatementLineDto(
    string Type,
    string Date,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);
