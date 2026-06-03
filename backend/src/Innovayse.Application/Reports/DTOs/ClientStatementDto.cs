namespace Innovayse.Application.Reports.DTOs;

/// <summary>One line in a client statement.</summary>
public record ClientStatementLineDto(
    string Type,
    string Date,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);

/// <summary>Full client statement result.</summary>
public record ClientStatementDto(
    int ClientId,
    string ClientName,
    decimal PreviousBalance,
    IReadOnlyList<ClientStatementLineDto> Lines,
    decimal EndingBalance);
