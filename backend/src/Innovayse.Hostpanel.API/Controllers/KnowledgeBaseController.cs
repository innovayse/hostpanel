using Innovayse.Hostpanel.Application.Support.Commands.CreateKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.CreateKbCategory;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteKbCategory;
using Innovayse.Hostpanel.Application.Support.Commands.PublishKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateKbArticle;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateKbCategory;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticle;
using Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticleBySlug;
using Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbArticles;
using Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbCategories;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The public, anonymous half of the knowledge base: published articles only.
/// </summary>
/// <remarks>
/// A separate controller rather than a relaxed authorization on <see cref="KnowledgeBaseAdminController"/>:
/// that class is admin surface end to end, and loosening its authorization for one read would put
/// a client-reachable action on the same type as every write.
/// </remarks>
[Route("api/knowledgebase")]
[AllowAnonymous]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class KnowledgeBaseController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public KnowledgeBaseController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the categories of the published knowledge base articles, each with its published article count.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the categories, ordered by name;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishedKbCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPublishedCategoriesAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<PublishedKbCategoryDto>>>(
            new ListPublishedKbCategoriesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Returns one page of published knowledge base articles, filtered by category (name or slug) and search term: title,
    /// plain-text excerpt and category of each, never the body.
    /// </summary>
    /// <param name="query">Category, search term and paging, bound from the query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 with <c>VALIDATION_ERROR</c> when the page or page size is out of range;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("articles")]
    [ProducesResponseType(typeof(PaginatedList<KbArticleSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListPublishedPageAsync([FromQuery] ListPublishedKbArticlesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<KbArticleSummaryDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single published knowledge base article.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the article; 404 with <c>KBARTICLE_NOT_FOUND</c> when the id names no row or names an unpublished draft: the
    /// two are answered alike so an anonymous caller cannot map the unpublished backlog by id; 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishedKbArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublishedByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PublishedKbArticleDto>>(new GetPublishedKbArticleQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single published knowledge base article by its slug.</summary>
    /// <remarks>
    /// Route matching ignores case, so the literal segments <c>articles</c> and <c>categories</c> (in any letter case) are
    /// served by their own actions, not by this one. A purely numeric segment never reaches this action: the route
    /// pattern refuses it (a negative look-ahead), so it is read as an id by <c>{id:int}</c>, and a number too large for
    /// an <see cref="int"/> matches no route at all and is answered 404 by routing. Without that look-ahead a number
    /// would match both routes and the request would fail as ambiguous. The slug is matched without regard to case.
    /// </remarks>
    /// <param name="slug">Article slug, in any letter case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the article, its slug in the canonical lower case; 404 with <c>KBARTICLE_NOT_FOUND</c> when no published
    /// article has the slug (an unpublished draft is answered like a missing one); 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{slug:regex(^(?![[0-9]]+$)[[a-zA-Z0-9]]+(-[[a-zA-Z0-9]]+)*$)}")]
    [ProducesResponseType(typeof(PublishedKbArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublishedBySlugAsync(string slug, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PublishedKbArticleDto>>(new GetPublishedKbArticleBySlugQuery(slug), ct);
        return HandleResult(outcome);
    }
}
