namespace Innovayse.Application.Services.Queries.GetServiceProductType;

/// <summary>Returns the product type for a given client service.</summary>
/// <param name="ServiceId">The client service primary key.</param>
public record GetServiceProductTypeQuery(int ServiceId);
