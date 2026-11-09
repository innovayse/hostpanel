using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.AddEmailForwardingRule;

/// <summary>
/// Adds an email forwarding rule to the domain aggregate and syncs the rule to the registrar.
/// The rule is persisted in the aggregate first so that a retry can recover from a registrar failure.
/// </summary>
public sealed class AddEmailForwardingRuleHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<AddEmailForwardingRuleHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public AddEmailForwardingRuleHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<AddEmailForwardingRuleHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="AddEmailForwardingRuleCommand"/>.
    /// </summary>
    /// <param name="cmd">The add email forwarding rule command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>EMAIL_FORWARDING_RULE_UPSTREAM_FAILED</c> when the registrar rejects the addition.
    /// </returns>
    public async Task<Result> HandleAsync(AddEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        domain.AddForwardingRule(cmd.Source, cmd.Destination);
        await _context.SaveChangesAsync(ct);

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.AddEmailForwardingRuleAsync(
            new EmailForwardingRuleChange(domain.Name, cmd.Source, cmd.Destination), ct);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar failed to add email forwarding rule for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.EMAIL_FORWARDING_RULE_UPSTREAM_FAILED));
        }

        return Result.Success();
    }
}
