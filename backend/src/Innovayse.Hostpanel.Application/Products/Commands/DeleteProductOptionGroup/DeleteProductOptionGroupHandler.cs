using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOptionGroup;

/// <summary>Removes an option group; the database removes its options with it.</summary>
public sealed class DeleteProductOptionGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public DeleteProductOptionGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The group to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c> when the id names no group.</returns>
    public async Task<Result> HandleAsync(DeleteProductOptionGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ProductOptionGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
        }

        _context.ProductOptionGroups.Remove(group);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
