namespace Innovayse.Domain.Services;

/// <summary>Lifecycle status of a cancellation request.</summary>
public enum CancellationStatus
{
    /// <summary>The cancellation request is pending and has not been processed.</summary>
    Open,

    /// <summary>The cancellation request has been processed and closed.</summary>
    Closed,
}
