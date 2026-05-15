namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing/{id}/pay and POST /api/me/billing/{id}/pay.</summary>
public sealed class PayInvoiceRequest
{
    /// <summary>Default ISO 4217 currency code used when none is specified.</summary>
    private const string DefaultCurrency = "USD";

    /// <summary>Gets the ISO 4217 currency code for the payment. Defaults to <c>USD</c>.</summary>
    public string Currency { get; init; } = DefaultCurrency;
}
