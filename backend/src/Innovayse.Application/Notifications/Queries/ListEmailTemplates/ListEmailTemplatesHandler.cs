namespace Innovayse.Application.Notifications.Queries.ListEmailTemplates;

using Innovayse.Application.Notifications.DTOs;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Returns all email templates.</summary>
public sealed class ListEmailTemplatesHandler(IEmailTemplateRepository repo)
{
    /// <summary>Handles <see cref="ListEmailTemplatesQuery"/>.</summary>
    /// <param name="query">The list email templates query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all <see cref="EmailTemplateDto"/> entries.</returns>
    public async Task<IReadOnlyList<EmailTemplateDto>> HandleAsync(ListEmailTemplatesQuery query, CancellationToken ct)
    {
        var templates = await repo.ListAsync(ct);

        return templates
            .Select(t => new EmailTemplateDto(t.Id, t.Slug, t.Subject, t.Body, t.Description, t.IsActive))
            .ToList()
            .AsReadOnly();
    }
}
