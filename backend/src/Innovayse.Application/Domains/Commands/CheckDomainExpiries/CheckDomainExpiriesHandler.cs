namespace Innovayse.Application.Domains.Commands.CheckDomainExpiries;

using Innovayse.Application.Domains.Commands.MarkDomainExpired;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Wolverine;

/// <summary>
/// Scheduled handler that finds all active domains past their expiry date
/// and dispatches <see cref="MarkDomainExpiredCommand"/> for each.
/// </summary>
public sealed class CheckDomainExpiriesHandler(IDomainRepository repo, IMessageBus bus)
{
    /// <summary>
    /// Handles <see cref="CheckDomainExpiriesCommand"/>.
    /// </summary>
    /// <param name="cmd">The check domain expiries command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(CheckDomainExpiriesCommand cmd, CancellationToken ct)
    {
        var expiredDomains = await repo.ListExpiringBeforeAsync(DateTimeOffset.UtcNow, ct);

        foreach (var domain in expiredDomains)
        {
            if (domain.Status == DomainStatus.Active)
            {
                await bus.InvokeAsync(new MarkDomainExpiredCommand(domain.Id), ct);
            }
        }
    }
}
