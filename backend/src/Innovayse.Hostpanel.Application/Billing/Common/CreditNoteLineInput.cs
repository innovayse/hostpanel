namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One line staff credit on a credit note, as it arrives on the issue command.</summary>
/// <param name="Description">What is credited, in words.</param>
/// <param name="UnitPrice">Credited price per unit, before tax; greater than 0.</param>
/// <param name="Quantity">Number of units credited; at least 1.</param>
public sealed record CreditNoteLineInput(string Description, decimal UnitPrice, int Quantity);
