using Innovayse.Hostpanel.Application.Products.Commands.CreateProduct;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProduct;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProducts;
using Innovayse.Hostpanel.Application.Products.Queries.ListCatalogProducts;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The catalogue's products: the storefront's plans and the admin panel's editable rows.</summary>
[Route("api/products")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ProductsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ProductsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// The staff view of the products, optionally filtered by group or active status, priced in the
    /// caller's currency. Carries the provisioning fields, so it is for staff only.
    /// </summary>
    /// <param name="query">The group/active filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the products; 400 when a filter is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] GetProductsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// The storefront catalogue: active products the caller can buy, priced in their currency, with
    /// no provisioning field. Anonymous: the storefront renders it before anyone has signed in.
    /// </summary>
    /// <param name="query">The group, several-group and single-product filters and the requested currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the products; 400 when a filter is refused; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListCatalogAsync([FromQuery] ListCatalogProductsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<CatalogProductDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new product in a product group.</summary>
    /// <param name="command">The product's details and prices.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new product's id; 400 when a field is refused (a malformed SKU, metadata that is
    /// not a JSON object or is over the size cap, a grace period out of range); 404 with
    /// <c>PRODUCT_GROUP_NOT_FOUND</c>; 409 with <c>PRODUCT_SKU_ALREADY_IN_USE</c> when another product
    /// holds the SKU; 401 without a session; 403 for a caller who is not an admin; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/products/{id}", id));
    }

    /// <summary>Updates an existing product's details and prices.</summary>
    /// <param name="id">Product primary key; wins over any id in the body.</param>
    /// <param name="command">The product's new details and prices.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a field is refused (a malformed SKU, metadata that is not a JSON object
    /// or is over the size cap, a grace period out of range); 404 with <c>PRODUCT_NOT_FOUND</c>; 409
    /// with <c>PRODUCT_SKU_ALREADY_IN_USE</c> when another product holds the SKU; 401 without a session;
    /// 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }
}
