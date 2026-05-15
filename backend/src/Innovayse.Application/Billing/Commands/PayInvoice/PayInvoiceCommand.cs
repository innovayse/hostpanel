namespace Innovayse.Application.Billing.Commands.PayInvoice;

/// <summary>Command to charge the client's payment method and mark the invoice paid.</summary>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="Currency">ISO 4217 currency code. Defaults to <see cref="DefaultCurrency"/>.</param>
public record PayInvoiceCommand(int InvoiceId, string Currency = PayInvoiceCommand.DefaultCurrency)
{
    /// <summary>The default ISO 4217 currency code used when none is specified.</summary>
    public const string DefaultCurrency = "USD";
}
