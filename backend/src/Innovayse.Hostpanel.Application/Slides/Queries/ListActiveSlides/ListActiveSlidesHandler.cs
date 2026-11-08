using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductSummaries;
using Innovayse.Hostpanel.Application.Slides.Common;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Slides.Queries.ListActiveSlides;

/// <summary>
/// Loads active slides, narrows them to the audience the caller belongs to, resolves the best-match translation for the
/// requested locale (a slide with no usable translation is left out), asks the Products module for the linked products' prices, and maps them to
/// <see cref="SlidePublicDto"/>.
/// </summary>
public sealed class ListActiveSlidesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The panel fallback locale, for content with no translation in the requested one.</summary>
    private readonly IOptions<LocaleOptions> _localeOptions;

    /// <summary>Dispatches the Products query that prices the linked products; this module does not read product tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Tells whether the caller is signed in, which decides the audience the slides are narrowed to.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="localeOptions">The panel fallback locale, for content with no translation in the requested one.</param>
    /// <param name="bus">Dispatches the Products query that prices the linked products.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="currentUser">Tells whether the caller is signed in.</param>
    public ListActiveSlidesHandler(IHostpanelDbContext context, IOptions<LocaleOptions> localeOptions, IMessageBus bus, IClock clock, ICurrentUserService currentUser)
    {
        _context = context;
        _localeOptions = localeOptions;
        _bus = bus;
        _clock = clock;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="query">The query; it carries no input: the audience comes from the caller and the locale is the request culture set from <c>X-Language</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Active slides meant for the caller (<c>All</c> plus <c>Authenticated</c> for a signed-in caller, <c>All</c> plus <c>Guest</c> otherwise), ordered by sort order
    /// ascending then by id, mapped with translation and pricing resolved. A slide with no translation at all is left out. Never refuses.
    /// </returns>
    public async Task<Result<IReadOnlyList<SlidePublicDto>>> HandleAsync(
        ListActiveSlidesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var slidesQuery = _context.Slides
            .AsNoTracking()
            .Where(s => s.IsActive
                && (s.VisibleFrom == null || s.VisibleFrom <= now)
                && (s.VisibleUntil == null || s.VisibleUntil >= now));

        // Decided here from the caller, never from the request: an anonymous visitor must not be able to ask for the
        // slides meant for signed-in clients, and a signed-in client does not see the guest-only offers.
        var audience = _currentUser.IsAuthenticated ? SlideAudience.Authenticated : SlideAudience.Guest;
        slidesQuery = slidesQuery.Where(s => s.TargetAudience == SlideAudience.All || s.TargetAudience == audience);

        var rows = await slidesQuery.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);

        var requested = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var defaultLocale = _localeOptions.Value.DefaultLocale;

        // A slide with no translation at all has no headline to show; it is left out, as an untranslated question is.
        var slides = rows
            .Select(s => (Slide: s, Translation: s.Translations.ForLocale(t => t.Locale, requested, defaultLocale)))
            .Where(x => x.Translation is not null)
            .ToList();

        var productIds = slides
            .Where(x => x.Slide.ProductId.HasValue)
            .Select(x => x.Slide.ProductId!.Value)
            .Distinct()
            .ToList();

        // A slide shows the linked product's price in the visitor's own currency; a cycle the
        // product does not sell in that currency stays null and the slide renders no figure.
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(
            new GetProductSummariesQuery(productIds), ct);
        var productMap = (products.Value ?? []).ToDictionary(p => p.Id);

        var dtos = slides.Select(x => MapToDto(x.Slide, x.Translation!, productMap)).ToList();
        return Result<IReadOnlyList<SlidePublicDto>>.Success(dtos);
    }

    /// <summary>
    /// Maps a <see cref="Slide"/> and its resolved translation to a <see cref="SlidePublicDto"/> with product pricing.
    /// </summary>
    /// <param name="slide">The slide aggregate to map.</param>
    /// <param name="translation">The translation resolved for the request language.</param>
    /// <param name="productMap">The linked products keyed by id, priced in the caller's currency.</param>
    /// <returns>A <see cref="SlidePublicDto"/> with the translation and pricing.</returns>
    private static SlidePublicDto MapToDto(
        Slide slide,
        SlideTranslation translation,
        Dictionary<int, ProductSummaryDto> productMap)
    {
        decimal? monthlyPrice = null;
        decimal? annualPrice = null;
        decimal? fromPrice = null;

        if (slide.ProductId.HasValue && productMap.TryGetValue(slide.ProductId.Value, out var product))
        {
            monthlyPrice = product.MonthlyPrice;
            annualPrice = product.AnnualPrice;
            fromPrice = product.FromPrice;
        }

        return new SlidePublicDto(
            slide.Id,
            slide.IconName,
            slide.BrandColor,
            slide.ImageUrl,
            slide.ProductId,
            slide.SortOrder,
            slide.TargetAudience,
            new SlideContentDto(translation.Title, translation.Tagline, translation.Description, translation.Features),
            new SlideLinksDto(slide.DemoUrl, slide.LearnMoreUrl),
            new SlideCallToActionDto(translation.CtaText, translation.CtaUrl),
            new SlidePricingDto(monthlyPrice, annualPrice, fromPrice));
    }
}
