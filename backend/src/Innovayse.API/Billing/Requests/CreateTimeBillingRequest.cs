namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/clients/{clientId}/billable-items/time-billing.</summary>
public sealed class CreateTimeBillingRequest
{
    /// <summary>Gets or initialises the list of time billing entries.</summary>
    public required IReadOnlyList<TimeBillingEntryRequest> Entries { get; init; }
}
