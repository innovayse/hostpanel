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
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Saves the article under a slug nobody else has.</summary>
    private readonly KbSlugSaver _slugs;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="slugs">Saves the article under a slug nobody else has.</param>
    public CreateKbArticleHandler(IHostpanelDbContext context, KbSlugSaver slugs)
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
        var article = await _slugs.SaveArticleAsync(
            title,
            slug =>
            {
                var created = KbArticle.Create(cmd.CategoryId, slug);
                created.ReplaceTranslations(cmd.Translations.ToContent());
                if (cmd.IsPublished)
                {
                    created.Publish();
                }

                return created;
            },
            ct);

        return Result.Success(article.Id);
    }
}
