using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// A planned maintenance window announced on the status page. While it is under way the components it affects show
/// as <see cref="StatusComponentState.Maintenance"/>. Its state follows the clock, so only the cancellation is stored.
/// Stored in the <c>StatusMaintenances</c> table.
/// </summary>
public sealed class StatusMaintenance : BaseEntity, IAggregateRoot
{
    /// <summary>Longest Title may be: matches the <c>StatusMaintenances.Title</c> column.</summary>
    public const int TitleMaxLength = 300;

    /// <summary>Longest Description may be: matches the <c>StatusMaintenances.Description</c> column.</summary>
    public const int DescriptionMaxLength = 4000;

    /// <summary>Backing field for <see cref="Components"/>.</summary>
    private readonly List<StatusMaintenanceComponent> _components = [];

    /// <summary>Gets the headline visitors see.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets what the visitors are told about the work, or <c>null</c>.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the UTC moment the window starts.</summary>
    public DateTimeOffset ScheduledStart { get; private set; }

    /// <summary>Gets the UTC moment the window ends.</summary>
    public DateTimeOffset ScheduledEnd { get; private set; }

    /// <summary>Gets the UTC moment the window was called off, or <c>null</c> when it was not.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Gets the components the window affects.</summary>
    public IReadOnlyList<StatusMaintenanceComponent> Components => _components.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusMaintenance() { }

    /// <summary>Announces a window.</summary>
    /// <param name="title">The headline visitors see.</param>
    /// <param name="description">What the visitors are told about the work, or <c>null</c>.</param>
    /// <param name="scheduledStart">The UTC moment the window starts.</param>
    /// <param name="scheduledEnd">The UTC moment the window ends.</param>
    /// <param name="componentIds">The ids of the components the window affects; repeated ids count once.</param>
    /// <returns>A new window.</returns>
    /// <exception cref="DomainRuleException">The title is empty, or the end is not after the start.</exception>
    public static StatusMaintenance Schedule(
        string title,
        string? description,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IReadOnlyCollection<int> componentIds)
    {
        var maintenance = new StatusMaintenance();
        maintenance.Apply(title, description, scheduledStart, scheduledEnd, componentIds);
        return maintenance;
    }

    /// <summary>Works out where the window stands at a moment.</summary>
    /// <param name="now">The moment to judge by.</param>
    /// <returns>The state of the window at that moment.</returns>
    public StatusMaintenanceState StateAt(DateTimeOffset now)
    {
        if (CancelledAt is not null)
        {
            return StatusMaintenanceState.Cancelled;
        }

        if (now >= ScheduledEnd)
        {
            return StatusMaintenanceState.Completed;
        }

        return now >= ScheduledStart ? StatusMaintenanceState.InProgress : StatusMaintenanceState.Scheduled;
    }

    /// <summary>Changes the announcement of a window that has not ended or been called off.</summary>
    /// <param name="title">The headline visitors see.</param>
    /// <param name="description">What the visitors are told about the work, or <c>null</c>.</param>
    /// <param name="scheduledStart">The UTC moment the window starts.</param>
    /// <param name="scheduledEnd">The UTC moment the window ends.</param>
    /// <param name="componentIds">The ids of the components the window affects; repeated ids count once.</param>
    /// <param name="now">The UTC moment of the change.</param>
    /// <exception cref="DomainRuleException">The window already ended or was called off, the title is empty, or the end is not after the start.</exception>
    public void Update(
        string title,
        string? description,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IReadOnlyCollection<int> componentIds,
        DateTimeOffset now)
    {
        EnsureOpen(now);
        Apply(title, description, scheduledStart, scheduledEnd, componentIds);
    }

    /// <summary>Calls off a window that has not ended or been called off.</summary>
    /// <param name="now">The UTC moment of the cancellation.</param>
    /// <exception cref="DomainRuleException">The window already ended or was called off.</exception>
    public void Cancel(DateTimeOffset now)
    {
        EnsureOpen(now);
        CancelledAt = now;
    }

    /// <summary>Refuses a change to a window that already ended or was called off.</summary>
    /// <param name="now">The moment to judge by.</param>
    /// <exception cref="DomainRuleException">The window already ended or was called off.</exception>
    private void EnsureOpen(DateTimeOffset now)
    {
        if (StateAt(now) is StatusMaintenanceState.Completed or StatusMaintenanceState.Cancelled)
        {
            throw new DomainRuleException(StatusPageErrors.MaintenanceAlreadyClosed, "The maintenance window already ended or was cancelled.");
        }
    }

    /// <summary>Checks the announcement and writes it, replacing the affected components.</summary>
    /// <param name="title">The headline visitors see.</param>
    /// <param name="description">What the visitors are told about the work, or <c>null</c>.</param>
    /// <param name="scheduledStart">The UTC moment the window starts.</param>
    /// <param name="scheduledEnd">The UTC moment the window ends.</param>
    /// <param name="componentIds">The ids of the components the window affects; repeated ids count once.</param>
    /// <exception cref="DomainRuleException">The title is empty, or the end is not after the start.</exception>
    private void Apply(
        string title,
        string? description,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IReadOnlyCollection<int> componentIds)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainRuleException(StatusPageErrors.MaintenanceTitleRequired, "A maintenance window needs a title.");
        }

        if (scheduledEnd <= scheduledStart)
        {
            throw new DomainRuleException(StatusPageErrors.MaintenanceWindowInvalid, "The window must end after it starts.");
        }

        Title = title;
        Description = description;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;

        var wanted = componentIds.ToHashSet();
        _components.RemoveAll(c => !wanted.Contains(c.ComponentId));
        foreach (var componentId in wanted.Except(_components.Select(c => c.ComponentId)).ToList())
        {
            _components.Add(StatusMaintenanceComponent.Create(componentId));
        }
    }
}
