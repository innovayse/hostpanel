namespace Innovayse.Application.Slides.Commands.UpdateSlide;

using System.Text.Json;
using Innovayse.Application.Common;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="UpdateSlideCommand"/> by loading the existing slide, applying
/// field updates, replacing all translations, and persisting the result.
/// </summary>
/// <param name="slideRepo">Slide repository for loading and persistence.</param>
/// <param name="uow">Unit of work for committing changes.</param>
public sealed class UpdateSlideHandler(ISlideRepository slideRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates an existing slide's fields and replaces its translations.
    /// </summary>
    /// <param name="cmd">The update slide command containing the new field values and translations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the slide with the given ID is not found.</exception>
    public async Task HandleAsync(UpdateSlideCommand cmd, CancellationToken ct)
    {
        var slide = await slideRepo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Slide {cmd.Id} not found.");

        slide.Update(
            cmd.IconName,
            cmd.BrandColor,
            cmd.ImageUrl,
            cmd.DemoUrl,
            cmd.LearnMoreUrl,
            cmd.ProductId,
            cmd.SortOrder,
            cmd.IsActive,
            cmd.TargetAudience,
            cmd.VisibleFrom,
            cmd.VisibleUntil);

        // Remove stale translations that are no longer in the command
        var incomingLocales = cmd.Translations.Select(t => t.Locale).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleLocales = slide.Translations
            .Select(t => t.Locale)
            .Where(l => !incomingLocales.Contains(l))
            .ToList();

        foreach (var locale in staleLocales)
        {
            slide.RemoveTranslation(locale);
        }

        // Apply all incoming translations
        foreach (var t in cmd.Translations)
        {
            var featuresJson = t.Features is not null
                ? JsonSerializer.Serialize(t.Features)
                : null;

            slide.SetTranslation(t.Locale, t.Title, t.Tagline, t.Description, featuresJson, t.CtaText, t.CtaUrl);
        }

        await uow.SaveChangesAsync(ct);
    }
}
