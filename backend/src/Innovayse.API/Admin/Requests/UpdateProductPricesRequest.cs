namespace Innovayse.API.Admin.Requests;

/// <summary>Request body for recomputing every product's prices in the named currencies from the base.</summary>
/// <param name="Currencies">ISO 4217 alpha codes to recompute; the base may not be among them.</param>
public record UpdateProductPricesRequest(IReadOnlyList<string> Currencies);
