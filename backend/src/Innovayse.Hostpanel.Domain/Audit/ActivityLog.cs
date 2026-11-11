using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Audit;

/// <summary>
/// Records a single staff action for audit purposes: one performed on a client account, or a
/// system-wide one (such as installing a plugin) that concerns no client.
/// </summary>
public sealed class ActivityLog : BaseEntity
{
    /// <summary>Gets the FK to the client this log entry belongs to; <see langword="null"/> for a system-wide action.</summary>
    public int? ClientId { get; private set; }

    /// <summary>Gets the human-readable description of the action performed.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the Identity user ID of the admin who performed the action.</summary>
    public string? AdminId { get; private set; }

    /// <summary>Gets the display name of the admin who performed the action.</summary>
    public string? AdminName { get; private set; }

    /// <summary>Gets the email address of the admin who performed the action.</summary>
    public string? AdminEmail { get; private set; }

    /// <summary>Gets the IP address from which the action was performed.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the UTC timestamp when the action was performed.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Initialises a new <see cref="ActivityLog"/> for EF Core materialisation.</summary>
    internal ActivityLog() { }

    /// <summary>Initialises an <see cref="ActivityLog"/> with the given identity.</summary>
    /// <param name="id">Entity identifier.</param>
    private ActivityLog(int id)
    {
        Id = id;
    }

    /// <summary>Moves the entry to the moment the action was really performed, for log lines seeded from history.</summary>
    /// <param name="performedAt">The moment the action was performed (UTC).</param>
    public void Backdate(DateTimeOffset performedAt) => CreatedAt = performedAt;

    /// <summary>Creates a new activity log entry.</summary>
    /// <param name="clientId">FK to the client account; <see langword="null"/> for a system-wide action.</param>
    /// <param name="description">Human-readable description of the action.</param>
    /// <param name="adminId">Identity user ID of the performing admin.</param>
    /// <param name="adminName">Display name of the performing admin.</param>
    /// <param name="adminEmail">Email of the performing admin.</param>
    /// <param name="ipAddress">IP address from which the action originated.</param>
    /// <returns>A new unsaved <see cref="ActivityLog"/> entry.</returns>
    public static ActivityLog Create(
        int? clientId, string description,
        string? adminId, string? adminName, string? adminEmail, string? ipAddress)
    {
        return new ActivityLog(0)
        {
            ClientId = clientId,
            Description = description,
            AdminId = adminId,
            AdminName = adminName,
            AdminEmail = adminEmail,
            IpAddress = ipAddress,
        };
    }
}
