namespace Innovayse.API.Billing.Requests;

/// <summary>Request to create a billable item.</summary>
public sealed class CreateBillableItemRequest
{
    /// <summary>Gets or sets the client ID.</summary>
    public int ClientId { get; set; }

    /// <summary>Gets or sets the human-readable description.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Gets or sets the unit price.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the currency code.</summary>
    public string Currency { get; set; } = null!;

    /// <summary>Gets or sets the item type (OneTime or Recurring).</summary>
    public string Type { get; set; } = null!;

    /// <summary>Gets or sets the recurring period (Monthly, Quarterly, Annual).</summary>
    public string? RecurringPeriod { get; set; }

    /// <summary>Gets or sets the next due date for recurring items.</summary>
    public DateTimeOffset? NextDueDate { get; set; }
}
