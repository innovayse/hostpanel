using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductOptionGroup;

/// <summary>Adds an option group to a product and persists it.</summary>
public sealed class CreateProductOptionGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public CreateProductOptionGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The group to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new group's id, or <c>PRODUCT_NOT_FOUND</c> when the product does not exist. A quantity range
    /// that does not fit the type is refused by the aggregate as <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateProductOptionGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var productExists = await _context.Products.AnyAsync(p => p.Id == cmd.ProductId, ct);
        if (!productExists)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        var group = ProductOptionGroup.Create(
            cmd.ProductId, cmd.Name, cmd.Type, cmd.IsRequired, cmd.DisplayOrder, cmd.MinQuantity, cmd.MaxQuantity);

        _context.ProductOptionGroups.Add(group);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(group.Id);
    }
}
