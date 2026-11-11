using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Extensions;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.FindEmailTemplate;

/// <summary>
/// Picks the version of a template a mail is rendered from: the recipient's language when that version is written, the
/// default language's otherwise, so a template nobody has translated yet still reaches the recipient.
/// </summary>
public sealed class FindEmailTemplateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public FindEmailTemplateHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The slug and the recipient's language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The template version, or <c>EMAIL_TEMPLATE_NOT_FOUND</c> when the slug has none in the recipient's language nor in the default language.</returns>
    public async Task<Result<EmailTemplateDto>> HandleAsync(FindEmailTemplateQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var language = SupportedLanguages.IsSupported(query.Language?.ToLowerInvariant())
            ? query.Language!.ToLowerInvariant()
            : SupportedLanguages.Default;
        var versions = await _context.EmailTemplates
            .AsNoTracking()
            .Where(t => t.Slug == query.Slug && (t.Language == language || t.Language == SupportedLanguages.Default))
            .SelectDto()
            .ToListAsync(ct);
        var template = versions.Find(t => t.Language == language) ?? versions.Find(t => t.Language == SupportedLanguages.Default);

        return template is null
            ? Result<EmailTemplateDto>.FailureCode(HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND)
            : Result<EmailTemplateDto>.Success(template);
    }
}
