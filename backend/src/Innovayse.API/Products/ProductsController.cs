namespace Innovayse.API.Products;

using Innovayse.Application.Products.Commands.CreateProduct;
using Innovayse.Application.Products.Commands.UpdateProduct;
using Innovayse.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing products.
/// GET list is public (storefront). Write operations require Admin role.
/// </summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns products, optionally filtered by group or active status.</summary>
    /// <param name="groupId">Optional group filter.</param>
    /// <param name="activeOnly">Filter to active products only. Defaults to <see langword="true"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of product DTOs.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAllAsync(
        [FromQuery] int? groupId = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductDto>>(
            new GetProductsQuery(groupId, activeOnly), ct);
        return Ok(result);
    }

    /// <summary>Creates a new product.</summary>
    /// <param name="cmd">Create command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new product ID.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateProductCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing product's details and prices.</summary>
    /// <param name="id">Product primary key.</param>
    /// <param name="cmd">Update command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd with { Id = id }, ct);
        return NoContent();
    }
}
