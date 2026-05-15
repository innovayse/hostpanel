namespace Innovayse.Domain.Services;

/// <summary>Specifies when a service cancellation should take effect.</summary>
public enum CancellationType
{
    /// <summary>Cancel the service immediately.</summary>
    Immediate,

    /// <summary>Cancel at the end of the current billing period.</summary>
    EndOfBillingPeriod,
}
