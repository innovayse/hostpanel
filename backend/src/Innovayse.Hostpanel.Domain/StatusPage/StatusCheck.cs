using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// The outcome of one automatic check of a <see cref="StatusComponent"/>: the raw record the uptime figures are
/// counted from. Stored in the <c>StatusChecks</c> table and pruned once it is older than the retention.
/// </summary>
public sealed class StatusCheck : BaseEntity
{
    /// <summary>Gets the id of the component that was checked.</summary>
    public int ComponentId { get; private set; }

    /// <summary>Gets the UTC moment the check ran.</summary>
    public DateTimeOffset CheckedAt { get; private set; }

    /// <summary>Gets whether the component answered as expected.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>Gets how long the answer took, in milliseconds.</summary>
    public int LatencyMs { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusCheck() { }

    /// <summary>Creates the record of a check.</summary>
    /// <param name="componentId">The id of the component that was checked.</param>
    /// <param name="checkedAt">The UTC moment the check ran.</param>
    /// <param name="succeeded">Whether the component answered as expected.</param>
    /// <param name="latencyMs">How long the answer took, in milliseconds.</param>
    /// <returns>A new record.</returns>
    internal static StatusCheck Create(int componentId, DateTimeOffset checkedAt, bool succeeded, int latencyMs)
    {
        return new StatusCheck { ComponentId = componentId, CheckedAt = checkedAt, Succeeded = succeeded, LatencyMs = latencyMs };
    }
}
