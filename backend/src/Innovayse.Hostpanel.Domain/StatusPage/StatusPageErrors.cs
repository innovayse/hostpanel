namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// Refusal codes the status page aggregates throw as <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so the resource coverage test still requires a sentence for each one.
/// </remarks>
public static class StatusPageErrors
{
    /// <summary>A group or component name is empty or whitespace (400): <see cref="StatusComponentGroup.Create"/> and <see cref="StatusComponentGroup.AddComponent"/>.</summary>
    public const string NameRequired = "STATUS_NAME_REQUIRED";

    /// <summary>A display order is negative (400): <see cref="StatusComponentGroup.Create"/> and <see cref="StatusComponentGroup.AddComponent"/>.</summary>
    public const string DisplayOrderNegative = "STATUS_DISPLAY_ORDER_NEGATIVE";

    /// <summary>An incident title is empty or whitespace (400): <see cref="StatusIncident.Open"/>.</summary>
    public const string IncidentTitleRequired = "STATUS_INCIDENT_TITLE_REQUIRED";

    /// <summary>An incident update message is empty or whitespace (400): <see cref="StatusIncident.Open"/> and <see cref="StatusIncident.PostUpdate"/>.</summary>
    public const string IncidentMessageRequired = "STATUS_INCIDENT_MESSAGE_REQUIRED";

    /// <summary>The incident is resolved and takes no more updates (409): <see cref="StatusIncident.PostUpdate"/>.</summary>
    public const string IncidentAlreadyResolved = "STATUS_INCIDENT_ALREADY_RESOLVED";

    /// <summary>Monitoring is enabled without an address to check (400): <see cref="StatusComponent.ConfigureMonitoring"/>.</summary>
    public const string CheckUrlRequired = "STATUS_CHECK_URL_REQUIRED";

    /// <summary>The failure threshold is outside 1 to <see cref="StatusComponent.MaxFailureThreshold"/> (400): <see cref="StatusComponent.ConfigureMonitoring"/>.</summary>
    public const string FailureThresholdInvalid = "STATUS_FAILURE_THRESHOLD_INVALID";

    /// <summary>A maintenance window has no title (400): <see cref="StatusMaintenance.Schedule"/> and <see cref="StatusMaintenance.Update"/>.</summary>
    public const string MaintenanceTitleRequired = "STATUS_MAINTENANCE_TITLE_REQUIRED";

    /// <summary>A maintenance window does not end after it starts (400): <see cref="StatusMaintenance.Schedule"/> and <see cref="StatusMaintenance.Update"/>.</summary>
    public const string MaintenanceWindowInvalid = "STATUS_MAINTENANCE_WINDOW_INVALID";

    /// <summary>The maintenance window already ended or was cancelled (409): <see cref="StatusMaintenance.Update"/> and <see cref="StatusMaintenance.Cancel"/>.</summary>
    public const string MaintenanceAlreadyClosed = "STATUS_MAINTENANCE_ALREADY_CLOSED";

    /// <summary>The group still has components and cannot be removed (409): <see cref="StatusComponentGroup.EnsureRemovable"/>.</summary>
    public const string GroupNotEmpty = "STATUS_COMPONENT_GROUP_NOT_EMPTY";
}
