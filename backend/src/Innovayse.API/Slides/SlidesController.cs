namespace Innovayse.API.Slides;

using Innovayse.Application.Slides.DTOs;
using Innovayse.Application.Slides.Queries.ListActiveSlides;
using Innovayse.Domain.Slides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Public read-only endpoints for homepage slide data.
/// Returns only active slides that are within their visibility window.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/slides")]
[AllowAnonymous]
public sealed class SlidesController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns all active slides visible at the current time, localised for the requested locale.
    /// </summary>
    /// <param name="audience">Optional audience filter. Accepts "All", "Guest", or "Authenticated". When omitted, all audiences are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of public slide DTOs ordered by SortOrder.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SlidePublicDto>>> ListAsync(
        [FromQuery] string? audience,
        CancellationToken ct)
    {
        var locale = Request.Headers["x-locale"].FirstOrDefault() ?? "en";

        SlideAudience? audienceEnum = null;
        if (!string.IsNullOrWhiteSpace(audience)
            && Enum.TryParse<SlideAudience>(audience, ignoreCase: true, out var parsed))
        {
            audienceEnum = parsed;
        }

        var result = await bus.InvokeAsync<IReadOnlyList<SlidePublicDto>>(
            new ListActiveSlidesQuery(locale, audienceEnum), ct);

        return Ok(result);
    }
}
