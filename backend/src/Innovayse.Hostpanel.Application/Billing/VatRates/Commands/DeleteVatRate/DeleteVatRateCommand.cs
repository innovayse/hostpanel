namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.DeleteVatRate;

/// <summary>Command to delete a VAT rate row. Issued invoices keep the rate they were taxed at.</summary>
/// <param name="Id">The identifier of the row to delete.</param>
public sealed record DeleteVatRateCommand(int Id);
