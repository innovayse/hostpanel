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
    /// <summary>How many times the save is tried when another request takes the slug first.</summary>
    private const int MaxSlugAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Allocates a slug nobody else has.</summary>
    private readonly KbSlugAllocator _slugs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="slugs">Allocates a slug nobody else has.</param>
    public CreateKbCategoryHandler(IHostpanelDbContext context, KbSlugAllocator slugs)
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

        for (var attempt = 1; ; attempt++)
        {
            var category = KbCategory.Create(
                command.Name,
                await _slugs.AllocateForCategoryAsync(command.Name, ct),
                command.Description,
                command.IsHidden,
                command.ParentCategoryId);

            _context.KbCategories.Add(category);
            try
            {
                await _context.SaveChangesAsync(ct);
                return Result<int>.Success(category.Id);
            }
            catch (DbUpdateException)
            {
                // Another request may have taken the slug between the allocation and the save: allocate again.
                // Any other failure, or the last attempt, is not ours to hide.
                if (attempt >= MaxSlugAttempts || !await SlugTakenAsync(category.Slug, ct))
                {
                    throw;
                }

                _context.KbCategories.Remove(category);
            }
        }
    }

    /// <summary>Whether a stored category already has the slug.</summary>
    /// <param name="slug">The slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the slug is taken.</returns>
    private Task<bool> SlugTakenAsync(string slug, CancellationToken ct) =>
        _context.KbCategories.AsNoTracking().AnyAsync(c => c.Slug == slug, ct);
}
