namespace Innovayse.API.Billing.Requests;

/// <summary>Request to create a quote.</summary>
public sealed class CreateQuoteRequest
{
    /// <summary>Gets or sets the client ID.</summary>
    public int ClientId { get; set; }

    /// <summary>Gets or sets the quote subject/title.</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Gets or sets the quote expiry / valid-until date.</summary>
    public DateTimeOffset ValidUntil { get; set; }

    /// <summary>Gets or sets optional notes or terms.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the proposal text displayed at the top of the quote.</summary>
    public string? ProposalText { get; set; }

    /// <summary>Gets or sets the customer-facing footer notes.</summary>
    public string? CustomerNotes { get; set; }

    /// <summary>Gets or sets the admin-only internal notes.</summary>
    public string? AdminNotes { get; set; }

    /// <summary>Gets or sets the line items for the quote.</summary>
    public List<CreateQuoteItemRequest> Items { get; set; } = [];
}
