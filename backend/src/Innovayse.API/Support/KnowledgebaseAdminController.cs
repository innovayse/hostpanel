namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Support.Commands.CreateKbArticle;
using Innovayse.Application.Support.Commands.DeleteKbArticle;
using Innovayse.Application.Support.Commands.PublishKbArticle;
using Innovayse.Application.Support.Commands.UpdateKbArticle;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.ListKbArticles;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing all knowledge base articles (including unpublished).
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/knowledgebase")]
[Authorize(Roles = Roles.Admin)]
public sealed class KnowledgebaseAdminController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all knowledge base articles, including unpublished ones.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all article DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbArticleDto>>> GetAllAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<KbArticleDto>>(
            new ListKbArticlesQuery(false), ct);
        return Ok(result);
    }

    /// <summary>Creates a new knowledge base article (unpublished by default).</summary>
    /// <param name="request">Article title, content, and category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new article ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateKbArticleRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateKbArticleCommand(request.Title, request.Content, request.Category), ct);
        return StatusCode(201, id);
    }

    /// <summary>Updates an existing knowledge base article.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="request">Updated title, content, and category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] CreateKbArticleRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateKbArticleCommand(id, request.Title, request.Content, request.Category), ct);
        return NoContent();
    }

    /// <summary>Permanently deletes a knowledge base article.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteKbArticleCommand(id), ct);
        return NoContent();
    }

    /// <summary>Publishes a knowledge base article, making it visible to clients.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPut("{id:int}/publish")]
    public async Task<IActionResult> PublishAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new PublishKbArticleCommand(id, true), ct);
        return Ok();
    }
}
