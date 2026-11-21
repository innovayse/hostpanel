using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductGroup;

/// <summary>Creates a new product group and persists it.</summary>
public sealed class CreateProductGroupHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public CreateProductGroupHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The group to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created product group id. This use case refuses nothing.</returns>
    public async Task<Result<int>> HandleAsync(CreateProductGroupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var group = ProductGroup.Create(cmd.Name, cmd.Description);
        if (cmd.AllowedPaymentMethods is not null)
        {
            group.SetAllowedPaymentMethods(cmd.AllowedPaymentMethods);
        }

        _context.ProductGroups.Add(group);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(group.Id);
    }
}
