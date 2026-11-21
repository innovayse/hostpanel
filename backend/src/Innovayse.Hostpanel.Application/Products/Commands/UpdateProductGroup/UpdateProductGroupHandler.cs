using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductGroup;

/// <summary>Updates an existing product group's name, description and allowed payment methods.</summary>
public sealed class UpdateProductGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public UpdateProductGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The group's new name, description and allowed payment methods.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>PRODUCT_GROUP_NOT_FOUND</c> when the id names no group.</returns>
    public async Task<Result> HandleAsync(UpdateProductGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = await _context.ProductGroups.FirstOrDefaultAsync(g => g.Id == cmd.Id, ct);
        if (group is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_GROUP_NOT_FOUND);
        }

        group.Update(cmd.Name, cmd.Description);

        // Null keeps the stored restriction, like every other optional member of an update: a caller that does not know
        // the field never lifts it. An empty list is the explicit way to lift it.
        if (cmd.AllowedPaymentMethods is not null)
        {
            group.SetAllowedPaymentMethods(cmd.AllowedPaymentMethods);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
