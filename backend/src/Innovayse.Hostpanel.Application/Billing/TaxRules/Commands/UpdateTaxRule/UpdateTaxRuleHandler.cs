using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.UpdateTaxRule;

/// <summary>Updates a tax rule, refusing a move onto a country and region another rule holds.</summary>
public sealed class UpdateTaxRuleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateTaxRuleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The rule to update and its new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, <c>TAX_RULE_NOT_FOUND</c> when the id names no rule, or <c>TAX_RULE_ALREADY_EXISTS</c> when another rule holds the place.</returns>
    public async Task<Result> HandleAsync(UpdateTaxRuleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rule = await _context.TaxRules.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (rule is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
        }

        rule.Update(cmd.Name, cmd.Country, cmd.Region, cmd.RatePercent, cmd.IsEnabled);
        if (await _context.TaxRules.AsNoTracking().ForPlace(rule.Country, rule.Region).AnyAsync(r => r.Id != rule.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.TAX_RULE_ALREADY_EXISTS);
        }

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
