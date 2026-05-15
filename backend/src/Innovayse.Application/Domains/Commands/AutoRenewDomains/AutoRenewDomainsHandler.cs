namespace Innovayse.Application.Domains.Commands.AutoRenewDomains;

using Innovayse.Application.Domains.Commands.RenewDomain;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Scheduled handler that auto-renews all eligible domains expiring within the next 30 days.
/// Failures per domain are logged and swallowed so a single failed renewal does not abort the batch.
/// </summary>
public sealed class AutoRenewDomainsHandler(IDomainRepository repo, IMessageBus bus, ILogger<AutoRenewDomainsHandler> logger)
{
    /// <summary>
    /// Handles <see cref="AutoRenewDomainsCommand"/>.
    /// </summary>
    /// <param name="cmd">The auto renew domains command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(AutoRenewDomainsCommand cmd, CancellationToken ct)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(30);
        var domains = await repo.ListAutoRenewDueAsync(threshold, ct);

        foreach (var domain in domains)
        {
            try
            {
                await bus.InvokeAsync(new RenewDomainCommand(domain.Id, 1), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Auto-renewal failed for domain {DomainId} ({DomainName}).",
                    domain.Id,
                    domain.Name);
            }
        }
    }
}
