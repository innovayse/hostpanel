namespace Innovayse.Application.Notifications.Queries.ListEmailLogs;

using Innovayse.Application.Notifications.DTOs;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Returns a paged list of email log entries ordered by send time descending.</summary>
public sealed class ListEmailLogsHandler(IEmailLogRepository repo)
{
    /// <summary>Handles <see cref="ListEmailLogsQuery"/>.</summary>
    /// <param name="query">The list email logs query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of <see cref="EmailLogDto"/> entries for the requested page.</returns>
    public async Task<IReadOnlyList<EmailLogDto>> HandleAsync(ListEmailLogsQuery query, CancellationToken ct)
    {
        var logs = await repo.ListAsync(query.Page, query.PageSize, ct);

        return logs
            .Select(l => new EmailLogDto(l.Id, l.To, l.Subject, l.SentAt, l.Success, l.Error))
            .ToList()
            .AsReadOnly();
    }
}
