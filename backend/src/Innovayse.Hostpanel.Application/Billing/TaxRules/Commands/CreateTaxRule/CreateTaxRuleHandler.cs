using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.CreateTaxRule;

/// <summary>Creates a tax rule, refusing a second rule for a country and region that has one.</summary>
public sealed class CreateTaxRuleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateTaxRuleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The rule's fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the new rule's id, or <c>TAX_RULE_ALREADY_EXISTS</c> when the country and region have a rule.</returns>
    public async Task<Result<int>> HandleAsync(CreateTaxRuleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rule = TaxRule.Create(cmd.Name, cmd.Country, cmd.Region, cmd.RatePercent, cmd.IsEnabled);
        if (await _context.TaxRules.ForPlace(rule.Country, rule.Region).AnyAsync(ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.TAX_RULE_ALREADY_EXISTS);
        }

        _context.TaxRules.Add(rule);
        await _context.SaveChangesAsync(ct);

        return Result.Success(rule.Id);
    }
}
