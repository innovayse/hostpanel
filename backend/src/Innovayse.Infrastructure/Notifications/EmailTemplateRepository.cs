namespace Innovayse.Infrastructure.Notifications;

using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IEmailTemplateRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class EmailTemplateRepository(AppDbContext db) : IEmailTemplateRepository
{
    /// <inheritdoc/>
    public async Task<EmailTemplate?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc/>
    public async Task<EmailTemplate?> FindBySlugAsync(string slug, CancellationToken ct) =>
        await db.EmailTemplates.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    /// <inheritdoc/>
    public void Add(EmailTemplate template) => db.EmailTemplates.Add(template);

    /// <inheritdoc/>
    public void Remove(EmailTemplate template) => db.EmailTemplates.Remove(template);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmailTemplate>> ListAsync(CancellationToken ct) =>
        await db.EmailTemplates.OrderBy(t => t.Slug).ToListAsync(ct);
}
