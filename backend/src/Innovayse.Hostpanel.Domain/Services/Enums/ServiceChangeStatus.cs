namespace Innovayse.Hostpanel.Domain.Services.Enums;

/// <summary>Lifecycle status of a service plan change request.</summary>
public enum ServiceChangeStatus
{
    /// <summary>The request waits for staff to approve or reject it.</summary>
    Pending = 0,

    /// <summary>The request is approved; the plan change is applied once its invoice is paid and the hosting panel has accepted it.</summary>
    Approved = 1,

    /// <summary>The service now runs on the new plan.</summary>
    Completed = 2,

    /// <summary>The request was turned down and the service stays on its plan.</summary>
    Rejected = 3,
}
