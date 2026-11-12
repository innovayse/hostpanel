using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.DeleteTaxRule;

/// <summary>Deletes a tax rule. An invoice stores its own rate, so no invoice refers to the rule.</summary>
public sealed class DeleteTaxRuleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteTaxRuleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The rule to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>TAX_RULE_NOT_FOUND</c> when the id names no rule.</returns>
    public async Task<Result> HandleAsync(DeleteTaxRuleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rule = await _context.TaxRules.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (rule is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TAX_RULE_NOT_FOUND);
        }

        _context.TaxRules.Remove(rule);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
