namespace Innovayse.Application.Domains.Commands.MarkDomainExpired;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Transitions an active domain to the expired state and raises the <c>DomainExpiredEvent</c> domain event.
/// Typically invoked by a scheduled job that checks for overdue expirations.
/// </summary>
public sealed class MarkDomainExpiredHandler(IDomainRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="MarkDomainExpiredCommand"/>.
    /// </summary>
    /// <param name="cmd">The mark domain expired command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the domain is not found.</exception>
    public async Task HandleAsync(MarkDomainExpiredCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.MarkExpired();
        await uow.SaveChangesAsync(ct);
    }
}
