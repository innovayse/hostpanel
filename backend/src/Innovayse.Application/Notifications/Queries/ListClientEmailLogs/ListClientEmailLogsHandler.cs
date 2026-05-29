namespace Innovayse.Application.Notifications.Queries.ListClientEmailLogs;

using Innovayse.Application.Common;
using Innovayse.Application.Notifications.DTOs;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Returns a paged list of email logs sent to a specific client.</summary>
public sealed class ListClientEmailLogsHandler(IEmailLogRepository repo)
{
    /// <summary>Handles <see cref="ListClientEmailLogsQuery"/>.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of email log DTOs for the client.</returns>
    public async Task<PagedResult<EmailLogDto>> HandleAsync(ListClientEmailLogsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListByClientIdAsync(query.ClientId, page, pageSize, ct);

        var dtos = items
            .Select(l => new EmailLogDto(l.Id, l.To, l.Subject, l.SentAt, l.Success, l.Error))
            .ToList();

        return new PagedResult<EmailLogDto>(dtos, total, page, pageSize);
    }
}
