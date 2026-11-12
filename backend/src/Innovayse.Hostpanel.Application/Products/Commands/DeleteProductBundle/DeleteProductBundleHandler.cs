using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductBundle;

/// <summary>Deletes a product bundle and its components.</summary>
public sealed class DeleteProductBundleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public DeleteProductBundleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The bundle to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>PRODUCT_BUNDLE_NOT_FOUND</c> when the id names no bundle.</returns>
    public async Task<Result> HandleAsync(DeleteProductBundleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var bundle = await _context.ProductBundles.FirstOrDefaultAsync(b => b.Id == cmd.Id, ct);
        if (bundle is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
        }

        // The components go with the bundle and the orders placed from it keep their rows with the link cleared,
        // both by the foreign keys, so nothing else is loaded here.
        _context.ProductBundles.Remove(bundle);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
