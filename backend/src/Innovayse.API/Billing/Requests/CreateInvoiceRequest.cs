namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing — creates a new invoice.</summary>
public sealed class CreateInvoiceRequest
{
    /// <summary>Gets or initialises the client ID to invoice.</summary>
    public required int ClientId { get; init; }

    /// <summary>Gets or initialises the payment due date (UTC).</summary>
    public required DateTimeOffset DueDate { get; init; }

    /// <summary>Gets or initialises the list of line items (must contain at least one).</summary>
    public required IReadOnlyList<CreateInvoiceItemRequest> Items { get; init; }
}
