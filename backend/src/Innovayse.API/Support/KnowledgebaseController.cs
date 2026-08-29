namespace Innovayse.API.Support;

using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.Queries.GetPublishedKbArticle;
using Innovayse.Application.Support.Queries.ListKbArticles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Public (unauthenticated) read-only endpoints for the knowledge base.
/// Returns only published articles.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/knowledgebase")]
[AllowAnonymous]
public sealed class KnowledgebaseController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all published knowledge base articles.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of published article DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbArticleDto>>> GetPublishedAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<KbArticleDto>>(
            new ListKbArticlesQuery(true), ct);
        return Ok(result);
    }

    /// <summary>Returns a single published knowledge base article.</summary>
    /// <param name="id">Article primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The article DTO. An unpublished draft and a nonexistent id answer identically: this route
    /// is anonymous and ids are sequential, so telling those apart would be a way to enumerate
    /// the unpublished backlog.
    /// </returns>
    /// <remarks>
    /// Dispatches <see cref="GetPublishedKbArticleQuery"/>, not <c>GetKbArticleQuery</c>. The
    /// latter reads any row regardless of published state and is the admin read; sending it from
    /// here served drafts to the public while the list above showed published rows only.
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<KbArticleDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<KbArticleDto>(new GetPublishedKbArticleQuery(id), ct);
        return Ok(dto);
    }
}
