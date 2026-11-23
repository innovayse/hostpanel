namespace Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

/// <summary>How one field of a hosting account compares between the platform and the panel.</summary>
public enum HostedAccountDriftKind
{
    /// <summary>The two agree.</summary>
    InSync = 1,

    /// <summary>They differ, and an existing operation can bring the two in step.</summary>
    Differs = 2,

    /// <summary>They differ or cannot be compared, and nothing in the platform changes that field.</summary>
    Unsupported = 3,
}
