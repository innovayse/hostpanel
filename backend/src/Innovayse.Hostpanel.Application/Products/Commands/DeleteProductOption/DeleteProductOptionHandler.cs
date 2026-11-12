using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOption;

/// <summary>Removes a choice from an option group.</summary>
public sealed class DeleteProductOptionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public DeleteProductOptionHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The choice to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>PRODUCT_OPTION_NOT_FOUND</c> when the id names no option.</returns>
    public async Task<Result> HandleAsync(DeleteProductOptionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.FindGroupOfOptionAsync(cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_OPTION_NOT_FOUND);
        }

        group.RemoveOption(cmd.Id);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
