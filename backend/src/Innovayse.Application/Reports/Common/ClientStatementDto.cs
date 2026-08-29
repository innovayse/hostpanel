namespace Innovayse.Application.Reports.Common;

/// <summary>Full client statement result.</summary>
public record ClientStatementDto(
    int ClientId,
    string ClientName,
    decimal PreviousBalance,
    IReadOnlyList<ClientStatementLineDto> Lines,
    decimal EndingBalance);
