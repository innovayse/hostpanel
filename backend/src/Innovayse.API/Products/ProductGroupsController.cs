namespace Innovayse.API.Products;

using Innovayse.Application.Products.Commands.CreateProductGroup;
using Innovayse.Application.Products.Commands.UpdateProductGroup;
using Innovayse.Application.Products.Queries.GetProductGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing product groups.
/// All write operations require the Admin role.
/// The GET list is public to support the storefront.
/// </summary>
[ApiController]
[Route("api/products/groups")]
public sealed class ProductGroupsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all product groups. Active-only for non-admin callers.</summary>
    /// <param name="activeOnly">Filter to active groups only. Defaults to <see langword="true"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product group DTOs.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductGroupDto>>> GetAllAsync(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductGroupDto>>(
            new GetProductGroupsQuery(activeOnly), ct);
        return Ok(result);
    }

    /// <summary>Creates a new product group.</summary>
    /// <param name="cmd">Create command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new group ID.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateProductGroupCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing product group.</summary>
    /// <param name="id">Product group primary key.</param>
    /// <param name="cmd">Update command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductGroupCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd with { Id = id }, ct);
        return NoContent();
    }
}
