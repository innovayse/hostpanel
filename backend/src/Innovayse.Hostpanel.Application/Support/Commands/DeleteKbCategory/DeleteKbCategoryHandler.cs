using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Commands.DeleteKbCategory;

/// <summary>Deletes a knowledge base category.</summary>
public sealed class DeleteKbCategoryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteKbCategoryHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The category to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>KBCATEGORY_NOT_FOUND</c> when the id names no category, or <c>KBCATEGORY_IN_USE</c>
    /// when it still has sub-categories or articles.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteKbCategoryCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var category = await _context.KbCategories.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (category is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
        }

        if (await _context.KbCategories.AnyAsync(c => c.ParentCategoryId == cmd.Id, ct)
            || await _context.KbArticles.AnyAsync(a => a.CategoryId == cmd.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.KBCATEGORY_IN_USE);
        }

        _context.KbCategories.Remove(category);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
