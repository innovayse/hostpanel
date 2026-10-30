namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// Refusal codes the <see cref="ClientService"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for each one in every language.
/// </remarks>
public static class ServicesErrors
{
    /// <summary>The service is not Active (400): <see cref="ClientService.Suspend"/>.</summary>
    public const string ServiceNotActive = "SERVICE_NOT_ACTIVE";

    /// <summary>The service is already terminated (400): <see cref="ClientService.Terminate"/>.</summary>
    public const string ServiceTerminateNotAllowed = "SERVICE_TERMINATE_NOT_ALLOWED";

    /// <summary>The service is not Suspended (400): <see cref="ClientService.Unsuspend"/>.</summary>
    public const string ServiceNotSuspended = "SERVICE_NOT_SUSPENDED";

    /// <summary>
    /// The service is not Pending (400), from <see cref="ClientService.AssignHostingAccount"/>,
    /// <see cref="ClientService.Activate"/> and <see cref="ClientService.ActivateDigital"/>.
    /// </summary>
    public const string ServiceNotPending = "SERVICE_NOT_PENDING";

    /// <summary>
    /// The asked-for status cannot be reached from the current one (400), when
    /// <see cref="ClientService.CanChangeStatusTo"/> refuses it.
    /// </summary>
    public const string ServiceStatusChangeNotAllowed = "SERVICE_STATUS_CHANGE_NOT_ALLOWED";

    /// <summary>
    /// A hold date without the suspension hold, or a termination reason without the end-of-cycle
    /// termination (400), from <see cref="ClientService.ConfigureAutomation"/>.
    /// </summary>
    public const string ServiceAutomationInconsistent = "SERVICE_AUTOMATION_INCONSISTENT";

    /// <summary>The service is not Terminated (409), from <see cref="ClientService.CorrectTerminationDate"/>.</summary>
    public const string ServiceNotTerminated = "SERVICE_NOT_TERMINATED";
}
