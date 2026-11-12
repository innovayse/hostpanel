using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponents;

/// <summary>Gives the components of a status page group the display order of a list of ids.</summary>
public sealed class ReorderStatusComponentsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ReorderStatusComponentsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The group and its component ids in the wanted order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>STATUS_ORDER_MISMATCH</c> when the ids are not exactly the group's components, each once. Nothing is
    /// changed on a mismatch. A group that does not exist has no components, so any list of ids mismatches and it is
    /// answered the same way, not as <c>STATUS_COMPONENT_GROUP_NOT_FOUND</c>.
    /// </returns>
    public async Task<Result> HandleAsync(ReorderStatusComponentsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var components = await _context.StatusComponents
            .Where(c => c.GroupId == cmd.GroupId)
            .ToDictionaryAsync(c => c.Id, ct);

        // A list that drops a component would leave it at an order that now clashes with the others, and one that
        // repeats an id would give it two positions, so only an exact permutation is taken.
        var isPermutation = cmd.ComponentIds.Count == components.Count
            && cmd.ComponentIds.Distinct().Count() == components.Count
            && cmd.ComponentIds.All(components.ContainsKey);
        if (!isPermutation)
        {
            return Result.FailureCode(HostpanelErrorCodes.STATUS_ORDER_MISMATCH);
        }

        for (var position = 0; position < cmd.ComponentIds.Count; position++)
        {
            components[cmd.ComponentIds[position]].Reorder(position);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
