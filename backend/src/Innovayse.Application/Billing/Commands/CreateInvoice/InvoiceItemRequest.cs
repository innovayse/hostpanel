namespace Innovayse.Application.Billing.Commands.CreateInvoice;

/// <summary>Represents a single line item submitted with <see cref="CreateInvoiceCommand"/>.</summary>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit (≥ 0).</param>
/// <param name="Quantity">Number of units (≥ 1).</param>
public record InvoiceItemRequest(string Description, decimal UnitPrice, int Quantity);
