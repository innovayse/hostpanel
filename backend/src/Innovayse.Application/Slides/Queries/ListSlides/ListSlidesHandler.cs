namespace Innovayse.Application.Slides.Queries.ListSlides;

using System.Text.Json;
using Innovayse.Application.Slides.DTOs;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Slides.Interfaces;

/// <summary>
/// Handles <see cref="ListSlidesQuery"/> by loading all slides, resolving product names
/// for linked slides, and mapping them to <see cref="SlideAdminDto"/>.
/// </summary>
/// <param name="slideRepo">Slide repository for listing all slides.</param>
/// <param name="productRepo">Product repository for resolving product names.</param>
public sealed class ListSlidesHandler(ISlideRepository slideRepo, IProductRepository productRepo)
{
    /// <summary>
    /// Returns all slides as admin DTOs with resolved product names and deserialized feature arrays.
    /// </summary>
    /// <param name="query">The list slides query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All slides ordered by SortOrder ascending, mapped to <see cref="SlideAdminDto"/>.</returns>
    public async Task<List<SlideAdminDto>> HandleAsync(ListSlidesQuery query, CancellationToken ct)
    {
        var slides = await slideRepo.ListAllAsync(ct);

        // Collect all distinct product IDs that need name resolution
        var productIds = slides
            .Where(s => s.ProductId.HasValue)
            .Select(s => s.ProductId!.Value)
            .Distinct()
            .ToList();

        var productNameMap = new Dictionary<int, string>();
        if (productIds.Count > 0)
        {
            var products = await productRepo.FindByIdsAsync(productIds, ct);
            foreach (var product in products)
            {
                productNameMap[product.Id] = product.Name;
            }
        }

        return slides.Select(s => MapToDto(s, productNameMap)).ToList();
    }

    /// <summary>
    /// Maps a <see cref="Slide"/> domain aggregate to a <see cref="SlideAdminDto"/>.
    /// </summary>
    /// <param name="slide">The slide aggregate to map.</param>
    /// <param name="productNameMap">Dictionary of product ID to product name for name resolution.</param>
    /// <returns>A fully populated <see cref="SlideAdminDto"/>.</returns>
    private static SlideAdminDto MapToDto(Slide slide, Dictionary<int, string> productNameMap)
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

        var productName = slide.ProductId.HasValue && productNameMap.TryGetValue(slide.ProductId.Value, out var name)
            ? name
            : null;

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
        if (featuresJson is null)
        {
            return null;
        }

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
