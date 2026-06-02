namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Support.Commands.CreateDownload;
using Innovayse.Application.Support.Commands.CreateDownloadCategory;
using Innovayse.Application.Support.Commands.DeleteDownload;
using Innovayse.Application.Support.Commands.DeleteDownloadCategory;
using Innovayse.Application.Support.Commands.UpdateDownload;
using Innovayse.Application.Support.Commands.UpdateDownloadCategory;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetDownload;
using Innovayse.Application.Support.Queries.ListDownloadCategories;
using Innovayse.Application.Support.Queries.ListDownloads;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing downloadable files and download categories.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/downloads")]
[Authorize(Roles = Roles.Admin)]
public sealed class DownloadsController(IMessageBus bus) : ControllerBase
{
    // ── Categories ──────────────────────────────────────────────────────

    /// <summary>Returns all download categories with download counts.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of category DTOs.</returns>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<DownloadCategoryDto>>> GetCategoriesAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DownloadCategoryDto>>(
            new ListDownloadCategoriesQuery(), ct);
        return Ok(result);
    }

    /// <summary>Creates a new download category.</summary>
    /// <param name="request">Category details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new category ID.</returns>
    [HttpPost("categories")]
    public async Task<ActionResult<int>> CreateCategoryAsync(
        [FromBody] CreateDownloadCategoryRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateDownloadCategoryCommand(
                request.Name,
                request.Description,
                request.IsHidden,
                request.ParentCategoryId),
            ct);
        return StatusCode(201, id);
    }

    /// <summary>Updates an existing download category.</summary>
    /// <param name="id">Category primary key.</param>
    /// <param name="request">Updated category details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategoryAsync(
        int id, [FromBody] UpdateDownloadCategoryRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateDownloadCategoryCommand(
                id,
                request.Name,
                request.Description,
                request.IsHidden,
                request.ParentCategoryId),
            ct);
        return NoContent();
    }

    /// <summary>Deletes a download category.</summary>
    /// <param name="id">Category primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteDownloadCategoryCommand(id), ct);
        return NoContent();
    }

    // ── Downloads ───────────────────────────────────────────────────────

    /// <summary>Returns downloads, optionally filtered by category.</summary>
    /// <param name="categoryId">Optional category ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of download DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DownloadDto>>> GetAllAsync(
        [FromQuery] int? categoryId, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DownloadDto>>(
            new ListDownloadsQuery(categoryId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single download by ID.</summary>
    /// <param name="id">Download primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching download DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DownloadDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<DownloadDto>(
            new GetDownloadQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Creates a new download entry.</summary>
    /// <param name="request">Download details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new download ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateDownloadRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateDownloadCommand(
                request.Title,
                request.Description,
                request.Type,
                request.Filename,
                request.CategoryId,
                request.ClientsOnly,
                request.ProductDownload,
                request.IsHidden),
            ct);
        return StatusCode(201, id);
    }

    /// <summary>Updates an existing download entry.</summary>
    /// <param name="id">Download primary key.</param>
    /// <param name="request">Updated download details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateDownloadRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateDownloadCommand(
                id,
                request.Title,
                request.Description,
                request.Type,
                request.Filename,
                request.CategoryId,
                request.ClientsOnly,
                request.ProductDownload,
                request.IsHidden),
            ct);
        return NoContent();
    }

    /// <summary>Deletes a download entry.</summary>
    /// <param name="id">Download primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteDownloadCommand(id), ct);
        return NoContent();
    }
}
