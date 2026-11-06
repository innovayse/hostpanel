namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The words the items table, the totals and the footer of a PDF document are printed with. An invoice prints
/// <see cref="English"/>; a credit note prints the language of its client.
/// </summary>
/// <param name="BillTo">Heading above the client name.</param>
/// <param name="Description">Items table heading for the description column.</param>
/// <param name="Quantity">Items table heading for the quantity column.</param>
/// <param name="UnitPrice">Items table heading for the unit price column.</param>
/// <param name="Amount">Items table heading for the amount column.</param>
/// <param name="Subtotal">Label of the sub total.</param>
/// <param name="Tax">Label of the tax line, before the percentage.</param>
/// <param name="Credit">Label of the credit line.</param>
/// <param name="Total">Label of the total.</param>
/// <param name="Page">Footer word before the page number.</param>
/// <param name="Of">Footer word between the page number and the page count.</param>
/// <param name="TaxId">Prefix of the tax identifier line of the seller.</param>
public sealed record DocumentPdfLabels(
    string BillTo,
    string Description,
    string Quantity,
    string UnitPrice,
    string Amount,
    string Subtotal,
    string Tax,
    string Credit,
    string Total,
    string Page,
    string Of,
    string TaxId)
{
    /// <summary>Gets the labels an invoice has always been printed with.</summary>
    public static DocumentPdfLabels English { get; } = new(
        "Bill to", "Description", "Qty", "Unit price", "Amount", "Subtotal", "Tax", "Credit", "Total", "Page", "of", "Tax ID");
}
