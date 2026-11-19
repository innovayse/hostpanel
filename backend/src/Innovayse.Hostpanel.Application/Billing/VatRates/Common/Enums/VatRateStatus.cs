namespace Innovayse.Hostpanel.Application.Billing.VatRates.Common.Enums;

/// <summary>Where a VAT rate row stands on a given day, among the rows of its country and kind. Values start at 1.</summary>
public enum VatRateStatus
{
    /// <summary>The row is in force: its first day has come and no later row of the same country and kind has started.</summary>
    Active = 1,

    /// <summary>A later row of the same country and kind has started, so this one no longer applies.</summary>
    Superseded = 2,

    /// <summary>The row's first day has not come yet.</summary>
    Upcoming = 3,
}
