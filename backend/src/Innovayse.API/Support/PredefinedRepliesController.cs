namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Support.Commands.CreatePredefinedReply;
using Innovayse.Application.Support.Commands.CreatePredefinedReplyCategory;
using Innovayse.Application.Support.Commands.DeletePredefinedReply;
using Innovayse.Application.Support.Commands.DeletePredefinedReplyCategory;
using Innovayse.Application.Support.Commands.UpdatePredefinedReply;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.ListPredefinedReplies;
using Innovayse.Application.Support.Queries.ListPredefinedReplyCategories;
using Innovayse.Application.Support.Queries.SearchPredefinedReplies;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing predefined reply categories and replies.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/predefined-replies")]
[Authorize(Roles = Roles.Admin)]
public sealed class PredefinedRepliesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all predefined reply categories with reply counts.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of category DTOs.</returns>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<PredefinedReplyCategoryDto>>> GetCategoriesAsync(
        CancellationToken ct)
    {
        var categories = await bus.InvokeAsync<IReadOnlyList<PredefinedReplyCategoryDto>>(
            new ListPredefinedReplyCategoriesQuery(), ct);
        return Ok(categories);
    }

    /// <summary>Creates a new predefined reply category.</summary>
    /// <param name="request">Category creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created category ID.</returns>
    [HttpPost("categories")]
    public async Task<ActionResult<int>> CreateCategoryAsync(
        [FromBody] CreatePredefinedReplyCategoryRequest request, CancellationToken ct)
    {
        var cmd = new CreatePredefinedReplyCategoryCommand(request.Name, request.ParentCategoryId);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Permanently deletes a predefined reply category.</summary>
    /// <param name="id">Category primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePredefinedReplyCategoryCommand(id), ct);
        return NoContent();
    }

    /// <summary>Returns all predefined replies, optionally filtered by category.</summary>
    /// <param name="categoryId">Optional category FK filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of reply DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PredefinedReplyDto>>> GetAllAsync(
        [FromQuery] int? categoryId = null,
        CancellationToken ct = default)
    {
        var replies = await bus.InvokeAsync<IReadOnlyList<PredefinedReplyDto>>(
            new ListPredefinedRepliesQuery(categoryId), ct);
        return Ok(replies);
    }

    /// <summary>Searches predefined replies by name or content.</summary>
    /// <param name="q">The search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching reply DTOs.</returns>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PredefinedReplyDto>>> SearchAsync(
        [FromQuery] string q,
        CancellationToken ct = default)
    {
        var replies = await bus.InvokeAsync<IReadOnlyList<PredefinedReplyDto>>(
            new SearchPredefinedRepliesQuery(q), ct);
        return Ok(replies);
    }

    /// <summary>Creates a new predefined reply.</summary>
    /// <param name="request">Reply creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created reply ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreatePredefinedReplyRequest request, CancellationToken ct)
    {
        var cmd = new CreatePredefinedReplyCommand(request.Name, request.Content, request.CategoryId);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing predefined reply.</summary>
    /// <param name="id">Reply primary key.</param>
    /// <param name="request">Updated reply fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdatePredefinedReplyRequest request, CancellationToken ct)
    {
        var cmd = new UpdatePredefinedReplyCommand(id, request.Name, request.Content, request.CategoryId);
        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>Permanently deletes a predefined reply.</summary>
    /// <param name="id">Reply primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePredefinedReplyCommand(id), ct);
        return NoContent();
    }
}
