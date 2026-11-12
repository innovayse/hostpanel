using Innovayse.Hostpanel.Application.Products.Commands.AddProductOption;
using Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOption;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProductOption;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The choices inside a product's option groups, each with the price it adds.</summary>
[Route("api/product-options")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ProductOptionsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ProductOptionsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Adds a choice to an option group.</summary>
    /// <param name="command">The choice's group, name, prices and position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new option's id; 400 with a field refusal, <c>PRODUCT_OPTION_COUNT_NOT_ALLOWED</c>,
    /// <c>PRODUCT_OPTION_PRICE_NOT_ALLOWED</c> or <c>PRODUCT_OPTION_PRICE_OUT_OF_RANGE</c>; 404 with
    /// <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddAsync([FromBody] AddProductOptionCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/product-options/{id}", id));
    }

    /// <summary>Updates a choice.</summary>
    /// <param name="id">Option primary key; wins over any id in the body.</param>
    /// <param name="command">The choice's new name, prices and position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with a field refusal, <c>PRODUCT_OPTION_PRICE_NOT_ALLOWED</c> or
    /// <c>PRODUCT_OPTION_PRICE_OUT_OF_RANGE</c>; 404 with <c>PRODUCT_OPTION_NOT_FOUND</c>; 401 without a
    /// session; 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateProductOptionCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a choice from its group.</summary>
    /// <param name="id">Option primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>PRODUCT_OPTION_NOT_FOUND</c>; 401 without a session; 403 for a caller who
    /// is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteProductOptionCommand(id), ct);
        return HandleResult(outcome);
    }
}
