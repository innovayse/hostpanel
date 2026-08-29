namespace Innovayse.API.Billing.Requests;

/// <summary>A single time billing entry in the request.</summary>
public sealed class TimeBillingEntryRequest
{
    /// <summary>Gets or initialises the optional FK to the related client service.</summary>
    public int? ServiceId { get; init; }

    /// <summary>Gets or initialises the human-readable description of the time entry.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the number of hours worked.</summary>
    public required decimal Hours { get; init; }

    /// <summary>Gets or initialises the hourly rate to charge.</summary>
    public required decimal Rate { get; init; }
}
