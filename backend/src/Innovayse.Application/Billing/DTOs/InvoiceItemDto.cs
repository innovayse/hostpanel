namespace Innovayse.Application.Billing.DTOs;

/// <summary>DTO representing a single line item on an invoice.</summary>
/// <param name="Id">Line item primary key.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="Amount">Line total (UnitPrice × Quantity).</param>
public record InvoiceItemDto(int Id, string Description, decimal UnitPrice, int Quantity, decimal Amount);
