namespace Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

/// <summary>What a panel sync did about one field of a hosting account.</summary>
public enum PanelSyncOutcome
{
    /// <summary>The platform and the panel already agreed, so nothing was done.</summary>
    InSync = 1,

    /// <summary>The field differed and the platform's value was applied to the panel (or, for the IP address, the panel's value was recorded).</summary>
    Applied = 2,

    /// <summary>The field differed and applying it failed; the failure is in the log.</summary>
    Failed = 3,

    /// <summary>The field cannot be reconciled: the panel does not report it, or the platform has no operation that changes it.</summary>
    Unsupported = 4,
}
