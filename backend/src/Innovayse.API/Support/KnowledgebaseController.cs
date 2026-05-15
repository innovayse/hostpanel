namespace Innovayse.API.Support;

using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetKbArticle;
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
    /// <returns>The article DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<KbArticleDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<KbArticleDto>(new GetKbArticleQuery(id), ct);
        return Ok(dto);
    }
}
