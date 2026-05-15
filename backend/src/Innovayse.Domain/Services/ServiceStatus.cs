namespace Innovayse.Domain.Services;

/// <summary>Lifecycle status of a client service.</summary>
public enum ServiceStatus
{
    /// <summary>Service ordered but not yet provisioned.</summary>
    Pending,

    /// <summary>Service is active and provisioned.</summary>
    Active,

    /// <summary>Service is temporarily suspended (e.g., non-payment).</summary>
    Suspended,

    /// <summary>Service has been permanently terminated.</summary>
    Terminated,
}
