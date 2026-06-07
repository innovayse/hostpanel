namespace Innovayse.Application.Slides.Queries.GetSlide;

using System.Text.Json;
using Innovayse.Application.Slides.DTOs;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="GetSlideQuery"/> by loading a single slide, resolving its
/// product name if applicable, and mapping it to <see cref="SlideAdminDto"/>.
/// </summary>
/// <param name="slideRepo">Slide repository for finding a slide by ID.</param>
/// <param name="productRepo">Product repository for resolving the product name.</param>
public sealed class GetSlideHandler(ISlideRepository slideRepo, IProductRepository productRepo)
{
    /// <summary>
    /// Returns the admin DTO for a single slide, or null if not found.
    /// </summary>
    /// <param name="query">The query containing the slide ID to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="SlideAdminDto"/> with all fields and translations, or
    /// <see langword="null"/> if no slide with the given ID exists.
    /// </returns>
    public async Task<SlideAdminDto?> HandleAsync(GetSlideQuery query, CancellationToken ct)
    {
        var slide = await slideRepo.FindByIdAsync(query.Id, ct);
        if (slide is null) return null;

        string? productName = null;
        if (slide.ProductId.HasValue)
        {
            var product = await productRepo.FindByIdAsync(slide.ProductId.Value, ct);
            productName = product?.Name;
        }

        return MapToDto(slide, productName);
    }

    /// <summary>
    /// Maps a <see cref="Slide"/> domain aggregate to a <see cref="SlideAdminDto"/>.
    /// </summary>
    /// <param name="slide">The slide aggregate to map.</param>
    /// <param name="productName">The resolved product name, or null.</param>
    /// <returns>A fully populated <see cref="SlideAdminDto"/>.</returns>
    private static SlideAdminDto MapToDto(Slide slide, string? productName)
    {
        var translations = slide.Translations
            .Select(t => new SlideTranslationDto(
                t.Locale,
                t.Title,
                t.Tagline,
                t.Description,
                DeserializeFeatures(t.Features),
                t.CtaText,
                t.CtaUrl))
            .ToList();

        return new SlideAdminDto(
            slide.Id,
            slide.IconName,
            slide.BrandColor,
            slide.ImageUrl,
            slide.DemoUrl,
            slide.LearnMoreUrl,
            slide.ProductId,
            productName,
            slide.SortOrder,
            slide.IsActive,
            slide.TargetAudience,
            slide.VisibleFrom,
            slide.VisibleUntil,
            slide.CreatedAt,
            translations);
    }

    /// <summary>
    /// Deserializes a JSON features string to a string array, returning null when the input is null or invalid.
    /// </summary>
    /// <param name="featuresJson">The JSON string representing the features array, or null.</param>
    /// <returns>Deserialized string array, or null if the input is null or cannot be parsed.</returns>
    private static string[]? DeserializeFeatures(string? featuresJson)
    {
        if (featuresJson is null) return null;

        try
        {
            return JsonSerializer.Deserialize<string[]>(featuresJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
