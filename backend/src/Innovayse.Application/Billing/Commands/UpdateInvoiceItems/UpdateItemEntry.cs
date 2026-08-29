namespace Innovayse.Application.Billing.Commands.UpdateInvoiceItems;

/// <summary>Describes a single item change within an <see cref="UpdateInvoiceItemsCommand"/>.</summary>
/// <param name="Id">Existing item ID; null for new items.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="IsDeleted">When true, the item will be removed from the invoice.</param>
public record UpdateItemEntry(int? Id, string Description, decimal UnitPrice, int Quantity, bool IsDeleted);
