namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for PUT /api/billing/{id}/options — updates invoice dates, payment method, and tax rate.</summary>
public sealed class UpdateInvoiceOptionsRequest
{
    /// <summary>Gets or initialises the invoice issue date (UTC).</summary>
    public required DateTimeOffset InvoiceDate { get; init; }

    /// <summary>Gets or initialises the payment due date (UTC).</summary>
    public required DateTimeOffset DueDate { get; init; }

    /// <summary>Gets or initialises the preferred payment method; null to clear.</summary>
    public string? PaymentMethod { get; init; }

    /// <summary>Gets or initialises the tax rate percentage (0–100).</summary>
    public required decimal TaxRate { get; init; }
}
