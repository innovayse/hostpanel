using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductFeature;

/// <summary>Updates an existing specification line.</summary>
public sealed class UpdateProductFeatureHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public UpdateProductFeatureHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The line's new label, value and position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>PRODUCT_FEATURE_NOT_FOUND</c> when the id names no line, or
    /// <c>PRODUCT_FEATURE_LABEL_DUPLICATE</c> when the rename lands on a label another line of the
    /// same product already uses.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateProductFeatureCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var feature = await _context.ProductFeatures.FirstOrDefaultAsync(f => f.Id == cmd.Id, ct);
        if (feature is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_FEATURE_NOT_FOUND);
        }

        // Renaming a line onto a label the product already uses would hide one of them
        // from the comparison table, which keys its rows on the label. This line itself
        // is excluded, so saving a line without renaming it is never blocked.
        if (await _context.ProductFeatureLabelExistsAsync(feature.ProductId, cmd.Label, cmd.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_FEATURE_LABEL_DUPLICATE);
        }

        feature.Update(cmd.Label, cmd.Value, cmd.SortOrder);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
