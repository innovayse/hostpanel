using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductFeature;

/// <summary>Adds a specification line to a product and persists it.</summary>
public sealed class CreateProductFeatureHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public CreateProductFeatureHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The specification line to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created feature line id, or <c>PRODUCT_NOT_FOUND</c> when the product does not
    /// exist, or <c>PRODUCT_FEATURE_LABEL_DUPLICATE</c> when the product already has a line under
    /// this label.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateProductFeatureCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var productExists = await _context.Products.AnyAsync(p => p.Id == cmd.ProductId, ct);
        if (!productExists)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        if (await _context.ProductFeatureLabelExistsAsync(cmd.ProductId, cmd.Label, excludingId: null, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_FEATURE_LABEL_DUPLICATE);
        }

        var feature = ProductFeature.Create(cmd.ProductId, cmd.Label, cmd.Value, cmd.SortOrder);

        _context.ProductFeatures.Add(feature);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(feature.Id);
    }
}
