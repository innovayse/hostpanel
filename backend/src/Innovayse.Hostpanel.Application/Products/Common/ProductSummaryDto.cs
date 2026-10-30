namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>A product's display name, its monthly and annual prices in the caller's billing currency and what a billed line keeps of it.</summary>
/// <param name="Id">The product the summary describes.</param>
/// <param name="Name">The product's display name.</param>
/// <param name="MonthlyPrice">The monthly price in the caller's currency; null when the product is not sold monthly in it.</param>
/// <param name="AnnualPrice">The annual price in the caller's currency; null when the product is not sold annually in it.</param>
/// <param name="Fulfilment">What an invoice line charging for the product keeps a copy of.</param>
public sealed record ProductSummaryDto(int Id, string Name, decimal? MonthlyPrice, decimal? AnnualPrice, ProductFulfilmentDto Fulfilment);
