namespace Innovayse.Domain.Email;

/// <summary>Lifecycle status of a business email domain.</summary>
public enum EmailDomainStatus
{
    /// <summary>DNS records have not yet been verified.</summary>
    PendingDns,

    /// <summary>DNS verified; domain is fully operational.</summary>
    Active,

    /// <summary>Domain has been administratively suspended.</summary>
    Suspended
}
