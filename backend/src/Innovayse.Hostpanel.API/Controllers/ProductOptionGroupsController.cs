using Innovayse.Hostpanel.Application.Products.Commands.CreateProductOptionGroup;
using Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOptionGroup;
using Innovayse.Hostpanel.Application.Products.Commands.ReorderProductOptionGroups;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOptionGroup;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProductOptions;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductOptionGroups;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The configurable option groups of a product, such as an operating system or extra storage, chosen when ordering.</summary>
[Route("api/product-option-groups")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ProductOptionGroupsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ProductOptionGroupsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>A product's option groups with their options, empty groups included, as the admin edits them.</summary>
    /// <param name="query">The product to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the groups in display order; 400 when the query fails validation; 401 without a session;
    /// 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductOptionGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] GetProductOptionGroupsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ProductOptionGroupDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>The groups a client is offered when ordering an active product; groups with no option are left out.</summary>
    /// <param name="query">The product being ordered.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the groups in display order; 400 when the query fails validation; 404 with
    /// <c>PRODUCT_NOT_FOUND</c> when the product does not exist or is not active; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("orderable")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ProductOptionGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListOrderableAsync(
        [FromQuery] GetOrderableProductOptionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ProductOptionGroupDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an empty option group to a product.</summary>
    /// <param name="command">The group's product, name, type, requirement, position and quantity range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new group's id; 400 with a field refusal or <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>;
    /// 404 with <c>PRODUCT_NOT_FOUND</c>; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateProductOptionGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/product-option-groups/{id}", id));
    }

    /// <summary>Updates an option group; its type cannot be changed.</summary>
    /// <param name="id">Option group primary key; wins over any id in the body.</param>
    /// <param name="command">The group's new name, requirement, position and quantity range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with a field refusal or <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>; 404 with
    /// <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductOptionGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Puts a product's option groups in the order of a list of ids.</summary>
    /// <param name="command">The product and all of its group ids in the wanted order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with a field refusal or <c>PRODUCT_OPTION_GROUP_ORDER_MISMATCH</c>; 401 without a
    /// session; 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("order")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReorderAsync(
        [FromBody] ReorderProductOptionGroupsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes an option group together with its options.</summary>
    /// <param name="id">Option group primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>; 401 without a session; 403 for a caller
    /// who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteProductOptionGroupCommand(id), ct);
        return HandleResult(outcome);
    }
}
