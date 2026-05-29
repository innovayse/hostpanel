namespace Innovayse.Domain.Billing;

/// <summary>Classification for billable items.</summary>
public enum BillableItemType
{
    /// <summary>One-time charge that is not recurring.</summary>
    OneTime,

    /// <summary>Recurring charge that repeats monthly, quarterly, or annually.</summary>
    Recurring,
}
