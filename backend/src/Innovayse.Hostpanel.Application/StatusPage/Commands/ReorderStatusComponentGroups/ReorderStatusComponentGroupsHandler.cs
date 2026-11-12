using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponentGroups;

/// <summary>Gives the status page groups the display order of a list of ids.</summary>
public sealed class ReorderStatusComponentGroupsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ReorderStatusComponentGroupsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group ids in the wanted order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>STATUS_ORDER_MISMATCH</c> when the ids are not exactly the groups, each once. Nothing is changed on a
    /// mismatch.
    /// </returns>
    public async Task<Result> HandleAsync(ReorderStatusComponentGroupsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var groups = await _context.StatusComponentGroups.ToDictionaryAsync(g => g.Id, ct);

        // A list that drops a group would leave it at an order that now clashes with the others, and one that repeats an
        // id would give it two positions, so only an exact permutation is taken.
        var isPermutation = cmd.GroupIds.Count == groups.Count
            && cmd.GroupIds.Distinct().Count() == groups.Count
            && cmd.GroupIds.All(groups.ContainsKey);
        if (!isPermutation)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_ORDER_MISMATCH);
        }

        for (var position = 0; position < cmd.GroupIds.Count; position++)
        {
            groups[cmd.GroupIds[position]].Reorder(position);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
