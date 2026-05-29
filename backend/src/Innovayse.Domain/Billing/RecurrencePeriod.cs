namespace Innovayse.Domain.Billing;

/// <summary>Time unit for recurring billable item intervals.</summary>
public enum RecurrencePeriod
{
    /// <summary>Recur every N days.</summary>
    Days,

    /// <summary>Recur every N weeks.</summary>
    Weeks,

    /// <summary>Recur every N months.</summary>
    Months,

    /// <summary>Recur every N years.</summary>
    Years,
}
