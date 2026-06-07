namespace Innovayse.Application.Services.Queries.GetServiceProductType;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services.Interfaces;

/// <summary>
/// Handles <see cref="GetServiceProductTypeQuery"/> by loading the service and returning
/// the product type of the associated product.
/// </summary>
/// <param name="serviceRepo">Client service repository.</param>
/// <param name="productRepo">Product repository.</param>
public sealed class GetServiceProductTypeHandler(
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo)
{
    /// <summary>
    /// Returns the <see cref="ProductType"/> for the product associated with the given service.
    /// </summary>
    /// <param name="qry">The query containing the service ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The product type, or <see cref="ProductType.SharedHosting"/> as the default fallback.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public async Task<ProductType> HandleAsync(GetServiceProductTypeQuery qry, CancellationToken ct)
    {
        var service = await serviceRepo.FindByIdAsync(qry.ServiceId, ct)
            ?? throw new InvalidOperationException($"ClientService {qry.ServiceId} not found.");

        var product = await productRepo.FindByIdAsync(service.ProductId, ct);

        return product?.Type ?? ProductType.SharedHosting;
    }
}
