using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteEmailForwardingRule;

/// <summary>
/// Removes an email forwarding rule from the domain aggregate and deletes it at the registrar.
/// The rule's source address is captured before removal so it can be sent to the registrar.
/// </summary>
public sealed class DeleteEmailForwardingRuleHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<DeleteEmailForwardingRuleHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public DeleteEmailForwardingRuleHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<DeleteEmailForwardingRuleHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="DeleteEmailForwardingRuleCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete email forwarding rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>EMAIL_FORWARDING_RULE_NOT_FOUND</c> when the rule id names no rule on that domain, or
    /// <c>EMAIL_FORWARDING_RULE_UPSTREAM_FAILED</c> when the registrar rejects the deletion.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        var rule = domain.EmailForwardingRules.FirstOrDefault(r => r.Id == cmd.RuleId!.Value);
        if (rule is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.EMAIL_FORWARDING_RULE_NOT_FOUND);
        }

        var source = rule.Source;

        domain.RemoveForwardingRule(cmd.RuleId!.Value);
        await _context.SaveChangesAsync(ct);

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.DeleteEmailForwardingRuleAsync(
            new EmailForwardingRuleRemoval(domain.Name, source), ct);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar failed to delete email forwarding rule for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.EMAIL_FORWARDING_RULE_UPSTREAM_FAILED));
        }

        return Result.Success();
    }
}
