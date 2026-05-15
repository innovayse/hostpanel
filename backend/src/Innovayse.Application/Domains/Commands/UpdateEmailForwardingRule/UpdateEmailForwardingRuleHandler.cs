namespace Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Updates an existing email forwarding rule in the domain aggregate and syncs the change to the registrar.
/// </summary>
public sealed class UpdateEmailForwardingRuleHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateEmailForwardingRuleCommand"/>.
    /// </summary>
    /// <param name="cmd">The update email forwarding rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found, the rule does not exist, or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(UpdateEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.UpdateForwardingRule(cmd.RuleId, cmd.Source, cmd.Destination, cmd.IsActive);
        await uow.SaveChangesAsync(ct);

        var result = await registrar.UpdateEmailForwardingRuleAsync(domain.Name, cmd.Source, cmd.Destination, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar failed to update email forwarding rule for '{domain.Name}': {result.ErrorMessage}");
        }
    }
}
