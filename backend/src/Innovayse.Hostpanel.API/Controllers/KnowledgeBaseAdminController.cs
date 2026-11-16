using Innovayse.Hostpanel.Application.Support.Commands.CreateKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.CreateKbCategory;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteKbCategory;
using Innovayse.Hostpanel.Application.Support.Commands.PublishKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateKbCategory;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Queries.GetKbArticle;
using Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles;
using Innovayse.Hostpanel.Application.Support.Queries.ListKbCategories;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for managing all knowledge base articles and categories, published or not.
/// </summary>
[Route("api/admin/knowledgebase")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class KnowledgeBaseAdminController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public KnowledgeBaseAdminController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a page of the knowledge base articles, drafts included, narrowed by a search and filters.</summary>
    /// <param name="query">The page, page size, search, category and published state to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the query fails validation; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<KbArticleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAllAsync([FromQuery] ListKbArticlesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<KbArticleDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single knowledge base article, published or not.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the article; 404 when the id names no article; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(KbArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<KbArticleDto>>(new GetKbArticleQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new knowledge base article (a draft unless the body sets <c>isPublished</c>).</summary>
    /// <param name="command">Article title, content, category and published flag.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new article's id; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateKbArticleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/knowledgebase/{id}", id));
    }

    /// <summary>Updates an existing knowledge base article.</summary>
    /// <param name="id">Article primary key; wins over any id in the body.</param>
    /// <param name="command">Updated title, content, and category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no article; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateKbArticleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a knowledge base article.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no article; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteKbArticleCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Publishes a knowledge base article, making it visible to clients.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no article; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PublishAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new PublishKbArticleCommand(id, true), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns all knowledge base categories with article counts.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with every category; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<KbCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListCategoriesAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<KbCategoryDto>>>(new ListKbCategoriesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new knowledge base category.</summary>
    /// <param name="command">Category details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new category's id; 404 when the parent category names no category; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("categories")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategoryAsync([FromBody] CreateKbCategoryCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/knowledgebase/categories/{id}", id));
    }

    /// <summary>Updates an existing knowledge base category.</summary>
    /// <param name="id">Category primary key; wins over any id in the body.</param>
    /// <param name="command">Updated category details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no category; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateCategoryAsync(
        int id, [FromBody] UpdateKbCategoryCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a knowledge base category.</summary>
    /// <param name="id">Category primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no category; 409 with <c>KBCATEGORY_IN_USE</c> when it still has sub-categories; 400 when the request is refused; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteKbCategoryCommand(id), ct);
        return HandleResult(outcome);
    }
}
