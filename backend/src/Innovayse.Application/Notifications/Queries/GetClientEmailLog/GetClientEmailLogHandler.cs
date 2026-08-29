namespace Innovayse.Application.Notifications.Queries.GetClientEmailLog;

using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Returns one email a client was sent.</summary>
/// <param name="repo">Email log persistence.</param>
public sealed class GetClientEmailLogHandler(IEmailLogRepository repo)
{
    /// <summary>Handles <see cref="GetClientEmailLogQuery"/>.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entry, or null when it does not exist or was sent to another client.</returns>
    /// <remarks>
    /// Null covers both cases deliberately — see <see cref="IEmailLogRepository.FindByClientIdAsync"/>.
    /// Telling the two apart would confirm that an entry exists to whoever guessed its id.
    /// </remarks>
    public async Task<EmailLogDetailDto?> HandleAsync(GetClientEmailLogQuery query, CancellationToken ct)
    {
        var log = await repo.FindByClientIdAsync(query.ClientId, query.EmailLogId, ct);

        return log is null
            ? null
            : new EmailLogDetailDto(log.Id, log.To, log.Subject, log.Body, log.SentAt, log.Success, log.Error);
    }
}
