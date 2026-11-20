using Innovayse.Hostpanel.Application.Testimonials.Common;

namespace Innovayse.Hostpanel.Application.Testimonials.Commands.UpdateTestimonial;

/// <summary>Command to update an existing testimonial and replace its translations.</summary>
/// <param name="Id">
/// The testimonial identifier to update; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> and the route overlay
/// (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">The client's display name.</param>
/// <param name="Company">The client's company, or <c>null</c></param>
/// <param name="AvatarUrl">The URL of the client's avatar image, or <c>null</c></param>
/// <param name="Rating">The star rating, from 1 to 5.</param>
/// <param name="SortOrder">Display order (ascending).</param>
/// <param name="IsActive">Whether the testimonial is shown on the storefront.</param>
/// <param name="PublishedAt">The date the testimonial was given, or <c>null</c> to fall back to the creation time.</param>
/// <param name="Translations">Per-locale translations keyed by locale code (<c>en</c>, <c>ru</c>, <c>hy</c>, <c>de</c>, <c>es</c>).</param>
public sealed record UpdateTestimonialCommand(
    int Id,
    string Name,
    string? Company,
    string? AvatarUrl,
    int Rating,
    int SortOrder,
    bool IsActive,
    DateTimeOffset? PublishedAt,
    Dictionary<string, TestimonialTranslationDto> Translations);
