using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOption;

/// <summary>Updates an existing choice of an option group.</summary>
public sealed class UpdateProductOptionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public UpdateProductOptionHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The choice's new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>PRODUCT_OPTION_NOT_FOUND</c> when the id names no option. The aggregate refuses a price
    /// on a text option and a modifier out of range, as <c>AddProductOption</c> does.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateProductOptionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.FindGroupOfOptionAsync(cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_NOT_FOUND);
        }

        group.UpdateOption(cmd.Id, cmd.Name, cmd.PriceModifier, cmd.PriceType, cmd.SetupFeeModifier, cmd.DisplayOrder);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
