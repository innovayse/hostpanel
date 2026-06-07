namespace Innovayse.Application.Slides.Commands.CreateSlide;

using System.Text.Json;
using Innovayse.Application.Common;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="CreateSlideCommand"/> by creating a new <see cref="Slide"/> aggregate,
/// applying all provided translations, and persisting the result.
/// </summary>
/// <param name="slideRepo">Slide repository for persistence.</param>
/// <param name="uow">Unit of work for committing changes.</param>
public sealed class CreateSlideHandler(ISlideRepository slideRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates and persists a new slide with its translations.
    /// </summary>
    /// <param name="cmd">The create slide command with all slide and translation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly assigned slide identifier.</returns>
    public async Task<int> HandleAsync(CreateSlideCommand cmd, CancellationToken ct)
    {
        var slide = Slide.Create(
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

        foreach (var t in cmd.Translations)
        {
            var featuresJson = t.Features is not null
                ? JsonSerializer.Serialize(t.Features)
                : null;

            slide.SetTranslation(t.Locale, t.Title, t.Tagline, t.Description, featuresJson, t.CtaText, t.CtaUrl);
        }

        slideRepo.Add(slide);
        await uow.SaveChangesAsync(ct);

        return slide.Id;
    }
}
