namespace Innovayse.Application.Domains.Commands.UpdateDomain;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Applies administrative and billing field updates to a domain aggregate and persists changes.
/// Optionally replaces the nameserver list if non-empty nameservers are provided.
/// </summary>
public sealed class UpdateDomainHandler(
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The update domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found.
    /// </exception>
    public async Task HandleAsync(UpdateDomainCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        var status = Enum.Parse<DomainStatus>(cmd.Status);
        var expiresAt = DateTimeOffset.Parse(cmd.ExpiresAt);
        var nextDueDate = DateTimeOffset.Parse(cmd.NextDueDate);

        domain.Update(
            cmd.FirstPaymentAmount, cmd.RecurringAmount, cmd.PaymentMethod,
            cmd.PromotionCode, cmd.SubscriptionId, cmd.AdminNotes,
            expiresAt, nextDueDate, cmd.RegistrationPeriod, status);

        var nameserverHosts = cmd.Nameservers
            .Where(ns => !string.IsNullOrWhiteSpace(ns))
            .ToList();

        if (nameserverHosts.Count > 0)
        {
            domain.SetNameservers(nameserverHosts);
        }

        await uow.SaveChangesAsync(ct);
    }
}
