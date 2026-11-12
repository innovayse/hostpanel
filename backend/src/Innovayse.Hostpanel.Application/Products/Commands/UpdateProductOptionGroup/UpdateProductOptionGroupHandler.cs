using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOptionGroup;

/// <summary>Updates an existing option group.</summary>
public sealed class UpdateProductOptionGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public UpdateProductOptionGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The group's new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c> when the id names no group. A quantity range that
    /// does not fit the group's type is refused by the aggregate as <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateProductOptionGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ProductOptionGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
        }

        group.Update(cmd.Name, cmd.IsRequired, cmd.DisplayOrder, cmd.MinQuantity, cmd.MaxQuantity);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
