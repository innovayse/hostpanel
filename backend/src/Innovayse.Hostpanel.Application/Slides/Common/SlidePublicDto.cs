using Innovayse.Hostpanel.Domain.Slides.Enums;

namespace Innovayse.Hostpanel.Application.Slides.Common;

/// <summary>
/// DTO representing a slide for public consumption, with the translation fields resolved
/// for the requested locale and optional pricing from a linked product.
/// </summary>
/// <param name="Id">Slide identifier.</param>
/// <param name="IconName">MDI icon name (e.g. "cloud-check").</param>
/// <param name="BrandColor">Hex brand color (e.g. "#0ea5e9").</param>
/// <param name="ImageUrl">Image URL or path.</param>
/// <param name="ProductId">Optional FK to a linked product.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="TargetAudience">Target audience for this slide.</param>
/// <param name="Content">Headline, subtitle, body and features in the resolved locale.</param>
/// <param name="Links">The demo and learn-more links.</param>
/// <param name="CallToAction">The CTA button label and link in the resolved locale.</param>
/// <param name="Pricing">Monthly and annual price of the linked product in the caller's currency.</param>
public sealed record SlidePublicDto(
    int Id,
    string IconName,
    string BrandColor,
    string ImageUrl,
    int? ProductId,
    int SortOrder,
    SlideAudience TargetAudience,
    SlideContentDto Content,
    SlideLinksDto Links,
    SlideCallToActionDto CallToAction,
    SlidePricingDto Pricing);
