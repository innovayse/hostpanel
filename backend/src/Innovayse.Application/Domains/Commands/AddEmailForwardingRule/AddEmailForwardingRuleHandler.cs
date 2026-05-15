namespace Innovayse.Application.Domains.Commands.AddEmailForwardingRule;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Adds an email forwarding rule to the domain aggregate and syncs the rule to the registrar.
/// The rule is persisted in the aggregate first so that a retry can recover from a registrar failure.
/// </summary>
public sealed class AddEmailForwardingRuleHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="AddEmailForwardingRuleCommand"/>.
    /// </summary>
    /// <param name="cmd">The add email forwarding rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the rule addition.
    /// </exception>
    public async Task HandleAsync(AddEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.AddForwardingRule(cmd.Source, cmd.Destination);
        await uow.SaveChangesAsync(ct);

        var result = await registrar.AddEmailForwardingRuleAsync(domain.Name, cmd.Source, cmd.Destination, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar failed to add email forwarding rule for '{domain.Name}': {result.ErrorMessage}");
        }
    }
}
