namespace Innovayse.API.Admin;

using Innovayse.Application.Slides.Commands.CreateSlide;
using Innovayse.Application.Slides.Commands.DeleteSlide;
using Innovayse.Application.Slides.Commands.UpdateSlide;
using Innovayse.Application.Slides.Commands.UpdateSlideOrder;
using Innovayse.Application.Slides.DTOs;
using Innovayse.Application.Slides.Queries.GetSlide;
using Innovayse.Application.Slides.Queries.ListSlides;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing homepage slides (CRUD + ordering).
/// All operations require the Admin role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="env">Web host environment for resolving file paths.</param>
[ApiController]
[Route("api/admin/slides")]
[Authorize(Roles = Roles.Admin)]
public sealed class SlidesController(IMessageBus bus, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>
    /// Returns all slides ordered by SortOrder for the admin panel.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all slides with their translations.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SlideAdminDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<SlideAdminDto>>(new ListSlidesQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single slide by ID with all translations.
    /// </summary>
    /// <param name="id">The slide identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The slide DTO, or 404 when not found.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SlideAdminDto>> GetAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<SlideAdminDto?>(new GetSlideQuery(id), ct);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates a new homepage slide.
    /// </summary>
    /// <param name="cmd">The create slide command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new slide's ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateSlideCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return CreatedAtAction(nameof(GetAsync), new { id }, id);
    }

    /// <summary>
    /// Updates an existing slide and replaces its translations.
    /// </summary>
    /// <param name="id">The slide identifier from the route.</param>
    /// <param name="cmd">The update slide command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success, or 400 Bad Request when the route ID does not match the command ID.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateSlideCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id)
        {
            return BadRequest("Route ID does not match command ID.");
        }

        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a slide by ID.
    /// </summary>
    /// <param name="id">The slide identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteSlideCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Uploads a slide image and returns the URL path.
    /// </summary>
    /// <param name="file">The image file to upload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative URL path to the uploaded image.</returns>
    [HttpPost("upload-image")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImageAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided." });
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { error = "Invalid file type. Allowed: JPEG, PNG, WebP, GIF, SVG." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { error = "File too large. Maximum 5MB." });
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "slides");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(new { url = $"/uploads/slides/{fileName}" });
    }

    /// <summary>
    /// Updates the display order for multiple slides in a single operation.
    /// </summary>
    /// <param name="cmd">The update order command containing slide ID and sort order pairs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("order")]
    public async Task<IActionResult> UpdateOrderAsync([FromBody] UpdateSlideOrderCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }
}
