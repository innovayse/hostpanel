using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetActiveDomainProduct;

/// <summary>
/// Handles <see cref="GetActiveDomainProductQuery"/>: finds the active product a domain order line
/// hangs off.
/// </summary>
/// <remarks>
/// Keyed on <see cref="ProductType.Domain"/>, which is what the type is for, never on a guess from the
/// product name. Products are ordered by name so an installation with several active domain products
/// always gets the same one.
/// </remarks>
public sealed class GetActiveDomainProductHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetActiveDomainProductHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The domain product to order against, or <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c> when this
    /// installation has no active domain product. That is a configuration gap rather than a caller
    /// error, but the caller still needs a sentence rather than a 500.
    /// </returns>
    public async Task<Result<DomainProductDto>> HandleAsync(GetActiveDomainProductQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active && p.Type == ProductType.Domain)
            .OrderBy(p => p.Name)
            .Select(p => new DomainProductDto(p.Id))
            .FirstOrDefaultAsync(ct);

        return product is null
            ? Result<DomainProductDto>.FailureCode(HostpanelErrorCodes.DOMAIN_PRODUCT_NOT_CONFIGURED)
            : Result.Success(product);
    }
}
