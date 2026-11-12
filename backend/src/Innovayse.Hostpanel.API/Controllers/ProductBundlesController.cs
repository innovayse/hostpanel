using Innovayse.Hostpanel.Application.Products.Commands.CreateProductBundle;
using Innovayse.Hostpanel.Application.Products.Commands.DeleteProductBundle;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProductBundle;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductBundle;
using Innovayse.Hostpanel.Application.Products.Queries.ListCatalogProductBundles;
using Innovayse.Hostpanel.Application.Products.Queries.ListProductBundles;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>The product bundles the catalogue offers: the storefront's list and the admin panel's editable rows.</summary>
[Route("api/products/bundles")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ProductBundlesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ProductBundlesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>The staff view of every product bundle, the ones switched off included.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the bundles; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductBundleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ProductBundleDto>>>(new ListProductBundlesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>The storefront catalogue's product bundles: active bundles only, with no staff field. Anonymous: the storefront renders them before anyone has signed in.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the active bundles; 429 when the rate limit is hit.</returns>
    [HttpGet("/api/public/product-bundles")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogProductBundleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCatalogAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<CatalogProductBundleDto>>>(
            new ListCatalogProductBundlesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>One product bundle with its components, for staff.</summary>
    /// <param name="id">Product bundle primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the bundle; 400 when the id is refused; 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c>; 401 without a session;
    /// 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ProductBundleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ProductBundleDto>>(new GetProductBundleQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a product bundle.</summary>
    /// <param name="command">The bundle's details and components.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new bundle's id; 400 when a field or a bundle rule is refused; 404 with <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c>
    /// when a component's product does not exist; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateProductBundleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/products/bundles/{id}", id));
    }

    /// <summary>Replaces a product bundle's details and components.</summary>
    /// <param name="id">Product bundle primary key; wins over any id in the body.</param>
    /// <param name="command">The bundle's new details and components.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a field or a bundle rule is refused; 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c> or
    /// <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c>; 401 without a session; 403 for a caller who is not an admin;
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
        int id, [FromBody] UpdateProductBundleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a product bundle. Orders placed from it are kept and lose the link.</summary>
    /// <param name="id">Product bundle primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when the id is refused; 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c>; 401 without a session;
    /// 403 for a caller who is not an admin; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteProductBundleCommand(id), ct);
        return HandleResult(outcome);
    }
}
