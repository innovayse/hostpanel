namespace Innovayse.Application.Slides.DTOs;

using Innovayse.Domain.Slides;

/// <summary>
/// DTO representing a slide for public consumption, with flattened translation fields
/// for the requested locale and optional pricing from a linked product.
/// </summary>
/// <param name="Id">Slide identifier.</param>
/// <param name="IconName">MDI icon name (e.g. "cloud-check").</param>
/// <param name="BrandColor">Hex brand color (e.g. "#0ea5e9").</param>
/// <param name="ImageUrl">Image URL or path.</param>
/// <param name="DemoUrl">Optional external demo link.</param>
/// <param name="LearnMoreUrl">Optional learn-more page link.</param>
/// <param name="ProductId">Optional FK to a linked product.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="TargetAudience">Target audience for this slide.</param>
/// <param name="Title">Slide headline in the resolved locale.</param>
/// <param name="Tagline">Optional subtitle in the resolved locale.</param>
/// <param name="Description">Optional body text in the resolved locale.</param>
/// <param name="Features">Optional array of feature strings in the resolved locale.</param>
/// <param name="CtaText">Optional CTA button label in the resolved locale.</param>
/// <param name="CtaUrl">Optional CTA button link in the resolved locale.</param>
/// <param name="MonthlyPrice">Monthly price from the linked product, or null if no product linked.</param>
/// <param name="AnnualPrice">Annual price from the linked product, or null if no product linked.</param>
public record SlidePublicDto(
    int Id,
    string IconName,
    string BrandColor,
    string ImageUrl,
    string? DemoUrl,
    string? LearnMoreUrl,
    int? ProductId,
    int SortOrder,
    SlideAudience TargetAudience,
    string Title,
    string? Tagline,
    string? Description,
    string[]? Features,
    string? CtaText,
    string? CtaUrl,
    decimal? MonthlyPrice,
    decimal? AnnualPrice);
