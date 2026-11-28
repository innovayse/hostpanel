namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>A row of the items table, every cell already formatted.</summary>
/// <param name="Description">Charge description; wraps within its column.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="UnitPrice">Price per unit with its currency symbols.</param>
/// <param name="Tax">The tax rate of the line, such as <c>20%</c>.</param>
/// <param name="Amount">The line total with its currency symbols.</param>
internal sealed record PdfItem(string Description, string Quantity, string UnitPrice, string Tax, string Amount);
