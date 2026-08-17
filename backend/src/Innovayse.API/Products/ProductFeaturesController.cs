namespace Innovayse.API.Products;

using Innovayse.Application.Products.Commands.CreateProductFeature;
using Innovayse.Application.Products.Commands.DeleteProductFeature;
using Innovayse.Application.Products.Commands.UpdateProductFeature;
using Innovayse.Application.Products.Queries.GetProductFeatures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Endpoints for the specification lines shown in the storefront's plan
/// comparison table. GET is public; write operations require the Admin role.
/// </summary>
[ApiController]
[Route("api/product-features")]
public sealed class ProductFeaturesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns specification lines, filtered by group or by product.</summary>
    /// <param name="groupId">Optional product group filter.</param>
    /// <param name="productId">Optional single-product filter.</param>
    /// <param name="activeOnly">Consider only active products. Defaults to <see langword="true"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of feature line DTOs.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductFeatureDto>>> GetAllAsync(
        [FromQuery] int? groupId = null,
        [FromQuery] int? productId = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<ProductFeatureDto>>(
            new GetProductFeaturesQuery(groupId, productId, activeOnly), ct);
        return Ok(result);
    }

    /// <summary>Adds a specification line to a product.</summary>
    /// <param name="cmd">Create command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new line's ID.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateProductFeatureCommand cmd, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates a specification line.</summary>
    /// <param name="id">Feature line primary key.</param>
    /// <param name="cmd">Update command body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductFeatureCommand cmd, CancellationToken ct)
    {
        await bus.InvokeAsync(cmd with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Removes a specification line.</summary>
    /// <param name="id">Feature line primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteProductFeatureCommand(id), ct);
        return NoContent();
    }
}
