using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Commands.CreateKbArticle;

/// <summary>Creates a new knowledge base article: gives it a slug from its default language title and saves it.</summary>
public sealed class CreateKbArticleHandler
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
    public CreateKbArticleHandler(IHostpanelDbContext context, KbSlugAllocator slugs)
    {
        _context = context;
        _slugs = slugs;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command carrying the category and the translations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new article's id, or <c>KBCATEGORY_NOT_FOUND</c> when the category does not exist.</returns>
    public async Task<Result<int>> HandleAsync(CreateKbArticleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!await _context.KbCategories.AnyAsync(c => c.Id == cmd.CategoryId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
        }

        var title = cmd.Translations[SupportedLanguages.Default].Title;
        for (var attempt = 1; ; attempt++)
        {
            var article = KbArticle.Create(cmd.CategoryId, await _slugs.AllocateForArticleAsync(title, ct));
            article.ReplaceTranslations(cmd.Translations.ToContent());
            if (cmd.IsPublished)
            {
                article.Publish();
            }

            _context.KbArticles.Add(article);
            try
            {
                await _context.SaveChangesAsync(ct);
                return Result.Success(article.Id);
            }
            catch (DbUpdateException)
            {
                // Another request may have taken the slug between the allocation and the save: allocate again.
                // Any other failure, or the last attempt, is not ours to hide.
                if (attempt >= MaxSlugAttempts || !await SlugTakenAsync(article.Slug, ct))
                {
                    throw;
                }

                _context.KbArticles.Remove(article);
            }
        }
    }

    /// <summary>Whether a stored article already has the slug.</summary>
    /// <param name="slug">The slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the slug is taken.</returns>
    private Task<bool> SlugTakenAsync(string slug, CancellationToken ct) =>
        _context.KbArticles.AsNoTracking().AnyAsync(a => a.Slug == slug, ct);
}
