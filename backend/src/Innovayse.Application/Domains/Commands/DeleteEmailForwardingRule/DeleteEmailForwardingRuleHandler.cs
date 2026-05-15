namespace Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Removes an email forwarding rule from the domain aggregate and deletes it at the registrar.
/// The rule's source address is captured before removal so it can be sent to the registrar.
/// </summary>
public sealed class DeleteEmailForwardingRuleHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteEmailForwardingRuleCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete email forwarding rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found, the rule does not exist, or the registrar rejects the deletion.
    /// </exception>
    public async Task HandleAsync(DeleteEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        var rule = domain.EmailForwardingRules.FirstOrDefault(r => r.Id == cmd.RuleId)
            ?? throw new InvalidOperationException(
                $"Email forwarding rule {cmd.RuleId} not found on domain '{domain.Name}'.");

        var source = rule.Source;

        domain.RemoveForwardingRule(cmd.RuleId);
        await uow.SaveChangesAsync(ct);

        var result = await registrar.DeleteEmailForwardingRuleAsync(domain.Name, source, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar failed to delete email forwarding rule for '{domain.Name}': {result.ErrorMessage}");
        }
    }
}
