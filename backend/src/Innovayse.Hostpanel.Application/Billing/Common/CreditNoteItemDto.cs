namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One line of a credit note.</summary>
/// <param name="Id">Line primary key.</param>
/// <param name="Description">What is credited.</param>
/// <param name="UnitPrice">Credited price per unit, before tax.</param>
/// <param name="Quantity">Number of units credited.</param>
/// <param name="Amount">Line total before tax.</param>
public sealed record CreditNoteItemDto(int Id, string Description, decimal UnitPrice, int Quantity, decimal Amount);
