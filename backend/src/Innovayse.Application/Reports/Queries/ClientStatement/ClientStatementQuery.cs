namespace Innovayse.Application.Reports.Queries.ClientStatement;

/// <summary>Query for a single client's account statement.</summary>
public record ClientStatementQuery(
    int ClientId,
    DateOnly? From = null,
    DateOnly? To = null);
