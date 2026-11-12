using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.ReorderProductOptionGroups;

/// <summary>Gives a product's option groups the display order of a list of ids.</summary>
public sealed class ReorderProductOptionGroupsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public ReorderProductOptionGroupsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The product and its group ids in the wanted order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>PRODUCT_OPTION_GROUP_ORDER_MISMATCH</c> when the ids are not exactly the product's
    /// groups, each once. Nothing is changed on a mismatch. A product that does not exist has no groups,
    /// so any list of ids mismatches and it is answered the same way, not as <c>PRODUCT_NOT_FOUND</c>.
    /// </returns>
    public async Task<Result> HandleAsync(ReorderProductOptionGroupsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var groups = await _context.ProductOptionGroups
            .Where(g => g.ProductId == cmd.ProductId)
            .ToDictionaryAsync(g => g.Id, ct);

        // A list that drops a group would leave it at an order that now clashes with the others, and
        // one that repeats an id would give it two positions, so only an exact permutation is taken.
        var isPermutation = cmd.GroupIds.Count == groups.Count
            && cmd.GroupIds.Distinct().Count() == groups.Count
            && cmd.GroupIds.All(groups.ContainsKey);
        if (!isPermutation)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_GROUP_ORDER_MISMATCH);
        }

        for (var position = 0; position < cmd.GroupIds.Count; position++)
        {
            groups[cmd.GroupIds[position]].Reorder(position);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
