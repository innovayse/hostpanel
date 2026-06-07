namespace Innovayse.Application.Slides.Queries.ListActiveSlides;

using System.Text.Json;
using Innovayse.Application.Slides.DTOs;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Slides;
using Innovayse.Domain.Slides.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Handles <see cref="ListActiveSlidesQuery"/> by loading active slides, filtering by audience,
/// resolving the best-match translation for the requested locale, joining product pricing,
/// and mapping to <see cref="SlidePublicDto"/>.
/// </summary>
/// <param name="slideRepo">Slide repository for listing active slides.</param>
/// <param name="productRepo">Product repository for resolving pricing data.</param>
/// <param name="configuration">Application configuration for reading <c>DefaultLocale</c>.</param>
public sealed class ListActiveSlidesHandler(
    ISlideRepository slideRepo,
    IProductRepository productRepo,
    IConfiguration configuration)
{
    /// <summary>
    /// Returns active slides visible now, filtered by audience, with resolved translations and pricing.
    /// </summary>
    /// <param name="query">The query containing the locale and optional audience filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Active slides matching the audience filter (or all if null), ordered by SortOrder ascending,
    /// mapped to <see cref="SlidePublicDto"/> with translation and pricing resolved.
    /// </returns>
    public async Task<List<SlidePublicDto>> HandleAsync(ListActiveSlidesQuery query, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var slides = await slideRepo.ListActiveAsync(now, ct);

        // Filter by audience when specified
        if (query.Audience.HasValue)
        {
            slides = slides
                .Where(s => s.TargetAudience == SlideAudience.All || s.TargetAudience == query.Audience.Value)
                .ToList();
        }

        // Collect all distinct product IDs for batch loading
        var productIds = slides
            .Where(s => s.ProductId.HasValue)
            .Select(s => s.ProductId!.Value)
            .Distinct()
            .ToList();

        var productMap = new Dictionary<int, (decimal Monthly, decimal Annual)>();
        if (productIds.Count > 0)
        {
            var products = await productRepo.FindByIdsAsync(productIds, ct);
            foreach (var product in products)
            {
                productMap[product.Id] = (product.MonthlyPrice, product.AnnualPrice);
            }
        }

        var defaultLocale = configuration["DefaultLocale"] ?? "en";

        return slides.Select(s => MapToDto(s, query.Locale, defaultLocale, productMap)).ToList();
    }

    /// <summary>
    /// Maps a <see cref="Slide"/> to a <see cref="SlidePublicDto"/> using locale resolution and product pricing.
    /// </summary>
    /// <param name="slide">The slide aggregate to map.</param>
    /// <param name="requestedLocale">The locale requested by the client.</param>
    /// <param name="defaultLocale">The application default locale as fallback.</param>
    /// <param name="productMap">Map of product ID to monthly and annual prices.</param>
    /// <returns>A <see cref="SlidePublicDto"/> with the best available translation and pricing.</returns>
    private static SlidePublicDto MapToDto(
        Slide slide,
        string requestedLocale,
        string defaultLocale,
        Dictionary<int, (decimal Monthly, decimal Annual)> productMap)
    {
        // Locale resolution: requested → default → first available
        var translation =
            slide.Translations.FirstOrDefault(t => string.Equals(t.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase))
            ?? slide.Translations.FirstOrDefault(t => string.Equals(t.Locale, defaultLocale, StringComparison.OrdinalIgnoreCase))
            ?? slide.Translations.FirstOrDefault();

        decimal? monthlyPrice = null;
        decimal? annualPrice = null;

        if (slide.ProductId.HasValue && productMap.TryGetValue(slide.ProductId.Value, out var pricing))
        {
            monthlyPrice = pricing.Monthly;
            annualPrice = pricing.Annual;
        }

        return new SlidePublicDto(
            slide.Id,
            slide.IconName,
            slide.BrandColor,
            slide.ImageUrl,
            slide.DemoUrl,
            slide.LearnMoreUrl,
            slide.ProductId,
            slide.SortOrder,
            slide.TargetAudience,
            translation?.Title ?? string.Empty,
            translation?.Tagline,
            translation?.Description,
            DeserializeFeatures(translation?.Features),
            translation?.CtaText,
            translation?.CtaUrl,
            monthlyPrice,
            annualPrice);
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
