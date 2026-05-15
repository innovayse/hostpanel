namespace Innovayse.Domain.Domains;

/// <summary>Lifecycle status of a registered domain.</summary>
public enum DomainStatus
{
    /// <summary>Registration has been requested but not yet confirmed by the registrar.</summary>
    PendingRegistration,

    /// <summary>Incoming transfer has been initiated but not yet completed.</summary>
    PendingTransfer,

    /// <summary>Domain is active and resolving normally.</summary>
    Active,

    /// <summary>Domain registration has expired and is no longer resolving.</summary>
    Expired,

    /// <summary>Domain is in the registrar redemption grace period — recoverable with a fee.</summary>
    Redemption,

    /// <summary>Domain has been transferred out to another registrar.</summary>
    Transferred,

    /// <summary>Domain registration was cancelled before activation.</summary>
    Cancelled,
}
