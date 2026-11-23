namespace Innovayse.Hostpanel.Application.Provisioning.Common.Enums;

/// <summary>A part of a hosting account that a panel sync compares between the platform and the panel.</summary>
public enum PanelSyncField
{
    /// <summary>Whether the account is active or suspended.</summary>
    Status = 1,

    /// <summary>The hosting package the account is on.</summary>
    Package = 2,

    /// <summary>The account's primary domain.</summary>
    Domain = 3,

    /// <summary>The account's password, which a panel never reports and a sync only pushes when asked.</summary>
    Password = 4,

    /// <summary>The IP address the account is served on.</summary>
    IpAddress = 5,
}
