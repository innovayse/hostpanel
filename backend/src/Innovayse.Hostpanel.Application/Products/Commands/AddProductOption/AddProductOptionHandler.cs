using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.AddProductOption;

/// <summary>Adds a choice to an option group and persists it.</summary>
public sealed class AddProductOptionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public AddProductOptionHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The choice to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new option's id, or <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c> when the group does not exist. The
    /// aggregate refuses a second option on a single-option type (<c>PRODUCT_OPTION_COUNT_NOT_ALLOWED</c>),
    /// a price on a text option (<c>PRODUCT_OPTION_PRICE_NOT_ALLOWED</c>) and a modifier out of range
    /// (<c>PRODUCT_OPTION_PRICE_OUT_OF_RANGE</c>).
    /// </returns>
    public async Task<Result<int>> HandleAsync(AddProductOptionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ProductOptionGroups
            .Include(g => g.Options)
            .FirstOrDefaultAsync(g => g.Id == cmd.GroupId, ct);
        if (group is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
        }

        var option = group.AddOption(cmd.Name, cmd.PriceModifier, cmd.PriceType, cmd.SetupFeeModifier, cmd.DisplayOrder);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(option.Id);
    }
}
