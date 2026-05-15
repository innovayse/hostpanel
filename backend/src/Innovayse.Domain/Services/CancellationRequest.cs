namespace Innovayse.Domain.Services;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a client's request to cancel one of their services.
/// Linked to a <see cref="ClientService"/> via <see cref="ServiceId"/>.
/// Stored in the <c>cancellation_requests</c> table.
/// </summary>
public sealed class CancellationRequest : Entity
{
    /// <summary>Gets the FK to the service being cancelled.</summary>
    public int ServiceId { get; private set; }

    /// <summary>Gets when the cancellation should take effect.</summary>
    public CancellationType Type { get; private set; }

    /// <summary>Gets the optional reason supplied by the client (max 2000 chars).</summary>
    public string? Reason { get; private set; }

    /// <summary>Gets the current lifecycle status of this request.</summary>
    public CancellationStatus Status { get; private set; }

    /// <summary>Gets the UTC timestamp when the request was submitted.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private CancellationRequest() : base(0) { }

    /// <summary>
    /// Creates a new open cancellation request.
    /// </summary>
    /// <param name="serviceId">FK to the service being cancelled.</param>
    /// <param name="type">When the cancellation should take effect.</param>
    /// <param name="reason">Optional client-supplied reason (max 2000 chars).</param>
    /// <returns>A new open <see cref="CancellationRequest"/>.</returns>
    public static CancellationRequest Create(int serviceId, CancellationType type, string? reason)
    {
        return new CancellationRequest
        {
            ServiceId = serviceId,
            Type = type,
            Reason = reason,
            Status = CancellationStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Closes this cancellation request, marking it as processed.
    /// </summary>
    public void Close()
    {
        Status = CancellationStatus.Closed;
    }
}
