namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for PUT /api/billing/{id}/notes — updates or clears invoice notes.</summary>
public sealed class UpdateInvoiceNotesRequest
{
    /// <summary>Gets or initialises the notes text; null to clear.</summary>
    public string? Notes { get; init; }
}
