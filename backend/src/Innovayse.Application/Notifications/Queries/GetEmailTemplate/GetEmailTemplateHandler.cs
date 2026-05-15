namespace Innovayse.Application.Notifications.Queries.GetEmailTemplate;

using Innovayse.Application.Notifications.DTOs;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Returns a single email template by its identifier.</summary>
public sealed class GetEmailTemplateHandler(IEmailTemplateRepository repo)
{
    /// <summary>Handles <see cref="GetEmailTemplateQuery"/>.</summary>
    /// <param name="query">The get email template query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="EmailTemplateDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no template with the given ID exists.</exception>
    public async Task<EmailTemplateDto> HandleAsync(GetEmailTemplateQuery query, CancellationToken ct)
    {
        var template = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Email template {query.Id} was not found.");

        return new EmailTemplateDto(
            template.Id,
            template.Slug,
            template.Subject,
            template.Body,
            template.Description,
            template.IsActive);
    }
}
