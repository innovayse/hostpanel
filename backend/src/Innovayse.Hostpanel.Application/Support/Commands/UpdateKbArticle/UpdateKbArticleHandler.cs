using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Commands.UpdateKbArticle;

/// <summary>Updates an existing knowledge base article's category and translations. Its slug stays.</summary>
public sealed class UpdateKbArticleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateKbArticleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The category and translations to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>KBARTICLE_NOT_FOUND</c> when the id names no article, or <c>KBCATEGORY_NOT_FOUND</c> when the category does not exist.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateKbArticleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var article = await _context.KbArticles.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (article is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
        }

        if (!await _context.KbCategories.AnyAsync(c => c.Id == cmd.CategoryId, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
        }

        article.MoveToCategory(cmd.CategoryId);
        article.ReplaceTranslations(cmd.Translations.ToContent());
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
