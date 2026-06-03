namespace Innovayse.Application.Reports.Queries.ClientStatement;

using Innovayse.Application.Reports.DTOs;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="ClientStatementQuery"/>.</summary>
public sealed class ClientStatementHandler(IReportRepository repo)
{
    /// <summary>Returns a client account statement.</summary>
    public Task<ClientStatementDto> HandleAsync(ClientStatementQuery query, CancellationToken ct)
        => repo.GetClientStatementAsync(query.ClientId, query.From, query.To, ct);
}
