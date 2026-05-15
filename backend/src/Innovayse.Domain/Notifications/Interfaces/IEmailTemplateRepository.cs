namespace Innovayse.Domain.Notifications.Interfaces;

/// <summary>Persistence contract for <see cref="EmailTemplate"/> aggregates.</summary>
public interface IEmailTemplateRepository
{
    /// <summary>Finds an email template by its primary key.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching template, or <see langword="null"/> if not found.</returns>
    Task<EmailTemplate?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Finds an email template by its unique slug.</summary>
    /// <param name="slug">The template slug.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching template, or <see langword="null"/> if not found.</returns>
    Task<EmailTemplate?> FindBySlugAsync(string slug, CancellationToken ct);

    /// <summary>Stages a new email template for insertion.</summary>
    /// <param name="template">The template to add.</param>
    void Add(EmailTemplate template);

    /// <summary>Removes an email template from the store.</summary>
    /// <param name="template">The template to remove.</param>
    void Remove(EmailTemplate template);

    /// <summary>Returns all email templates.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all templates.</returns>
    Task<IReadOnlyList<EmailTemplate>> ListAsync(CancellationToken ct);
}
