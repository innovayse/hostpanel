namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a network issue or maintenance event that affects services.
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class NetworkIssue : Entity
{
    /// <summary>Gets the issue title (max 500 characters).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the type of network issue.</summary>
    public NetworkIssueType Type { get; private set; }

    /// <summary>Gets the affected server name, if applicable (max 255 characters).</summary>
    public string? Server { get; private set; }

    /// <summary>Gets the priority level of this issue.</summary>
    public NetworkIssuePriority Priority { get; private set; }

    /// <summary>Gets the current lifecycle status of this issue.</summary>
    public NetworkIssueStatus Status { get; private set; }

    /// <summary>Gets the UTC timestamp when the issue started.</summary>
    public DateTimeOffset StartDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the issue was resolved, if applicable.</summary>
    public DateTimeOffset? EndDate { get; private set; }

    /// <summary>Gets the HTML description of the issue.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when this record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private NetworkIssue() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="NetworkIssue"/> in <see cref="NetworkIssueStatus.Reported"/> status.
    /// </summary>
    /// <param name="title">The issue title.</param>
    /// <param name="type">The type of network issue.</param>
    /// <param name="server">The affected server name, or <see langword="null"/>.</param>
    /// <param name="priority">The priority level.</param>
    /// <param name="startDate">The UTC timestamp when the issue started.</param>
    /// <param name="description">The HTML description of the issue.</param>
    /// <returns>A new <see cref="NetworkIssue"/> in <see cref="NetworkIssueStatus.Reported"/> state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> or <paramref name="description"/> is null or whitespace.</exception>
    public static NetworkIssue Create(
        string title,
        NetworkIssueType type,
        string? server,
        NetworkIssuePriority priority,
        DateTimeOffset startDate,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new NetworkIssue
        {
            Title = title,
            Type = type,
            Server = server,
            Priority = priority,
            Status = NetworkIssueStatus.Reported,
            StartDate = startDate,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Updates the mutable fields of this network issue.
    /// </summary>
    /// <param name="title">The new issue title.</param>
    /// <param name="type">The new type.</param>
    /// <param name="server">The new affected server name, or <see langword="null"/>.</param>
    /// <param name="priority">The new priority level.</param>
    /// <param name="status">The new lifecycle status.</param>
    /// <param name="startDate">The new start date.</param>
    /// <param name="endDate">The new end date, or <see langword="null"/>.</param>
    /// <param name="description">The new HTML description.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> or <paramref name="description"/> is null or whitespace.</exception>
    public void Update(
        string title,
        NetworkIssueType type,
        string? server,
        NetworkIssuePriority priority,
        NetworkIssueStatus status,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Title = title;
        Type = type;
        Server = server;
        Priority = priority;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
    }

    /// <summary>
    /// Resolves this network issue by setting <see cref="Status"/> to <see cref="NetworkIssueStatus.Resolved"/>
    /// and <see cref="EndDate"/> to the current UTC time.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the issue is already resolved.</exception>
    public void Resolve()
    {
        if (Status == NetworkIssueStatus.Resolved)
        {
            throw new InvalidOperationException("Network issue is already resolved.");
        }

        Status = NetworkIssueStatus.Resolved;
        EndDate = DateTimeOffset.UtcNow;
    }
}
