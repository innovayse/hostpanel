using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Notifications;

namespace Innovayse.Hostpanel.Application.Notifications.Extensions;

/// <summary>
/// The one way an email log list narrows its entries: the search text on the subject and the recipient, the outcome and the
/// period it was sent in, all in SQL ahead of ordering and paging, so the staff log and a client's mail history cannot differ.
/// </summary>
public static class EmailLogFilterExtensions
{
    /// <summary>Keeps the log entries the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The email log list query.</typeparam>
    /// <param name="logs">The log entries to narrow.</param>
    /// <param name="query">The search text and the email log filters.</param>
    /// <returns>The narrowed log entries.</returns>
    public static IQueryable<EmailLog> Matching<T>(this IQueryable<EmailLog> logs, T query)
        where T : IListQuery, IEmailLogListFilters
    {
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(query);

        logs = logs.Search(query.Search, l => l.Subject, l => l.To);

        if (query.Success is { } success)
        {
            logs = logs.Where(l => l.Success == success);
        }

        return logs.InPeriod(l => l.SentAt, query.From, query.To);
    }
}
