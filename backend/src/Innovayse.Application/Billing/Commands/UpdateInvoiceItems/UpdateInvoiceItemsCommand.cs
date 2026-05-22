namespace Innovayse.Application.Billing.Commands.UpdateInvoiceItems;

/// <summary>Command to add, update, or remove line items on an invoice.</summary>
/// <param name="InvoiceId">The invoice to modify.</param>
/// <param name="Items">The item entries describing changes.</param>
public record UpdateInvoiceItemsCommand(int InvoiceId, IReadOnlyList<UpdateItemEntry> Items);

/// <summary>Describes a single item change within an <see cref="UpdateInvoiceItemsCommand"/>.</summary>
/// <param name="Id">Existing item ID; null for new items.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="IsDeleted">When true, the item will be removed from the invoice.</param>
public record UpdateItemEntry(int? Id, string Description, decimal UnitPrice, int Quantity, bool IsDeleted);
