using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Commands.CreateKbCategory;

/// <summary>Creates a knowledge base category: gives it a slug from its name and saves it.</summary>
public sealed class CreateKbCategoryHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Saves the category under a slug nobody else has.</summary>
    private readonly KbSlugSaver _slugs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="slugs">Saves the category under a slug nobody else has.</param>
    public CreateKbCategoryHandler(IHostpanelDbContext context, KbSlugSaver slugs)
    {
        _context = context;
        _slugs = slugs;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="command">The category to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new category's id, or <c>KBCATEGORY_NOT_FOUND</c> when the parent category does not exist.</returns>
    public async Task<Result<int>> HandleAsync(CreateKbCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ParentCategoryId is { } parentId
            && !await _context.KbCategories.AnyAsync(c => c.Id == parentId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
        }

        var category = await _slugs.SaveCategoryAsync(
            command.Name,
            slug => KbCategory.Create(command.Name, slug, command.Description, command.IsHidden, command.ParentCategoryId),
            ct);

        return Result<int>.Success(category.Id);
    }
}
