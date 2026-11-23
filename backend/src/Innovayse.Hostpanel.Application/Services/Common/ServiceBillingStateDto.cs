using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// The parts of a service's state that decide whether billing may suspend it for an overdue invoice
/// or lift such a suspension after payment.
/// </summary>
/// <param name="Id">The service.</param>
/// <param name="Status">Its lifecycle status.</param>
/// <param name="SuspensionSource">Why it is suspended, or <see langword="null"/> when it is not.</param>
/// <param name="OverrideAutoSuspend">Whether staff opted it out of automatic suspension.</param>
/// <param name="SuspendUntil">Until when automatic suspension is held off, if set.</param>
/// <param name="AutoRenew">Whether billing looks after the service at all; <see langword="false"/> means staff bill it by hand.</param>
/// <param name="InTrial">Whether a free trial is still running on it (active); billing never suspends such a service.</param>
public sealed record ServiceBillingStateDto(
    int Id,
    ServiceStatus Status,
    SuspensionSource? SuspensionSource,
    bool OverrideAutoSuspend,
    DateTimeOffset? SuspendUntil,
    bool AutoRenew,
    bool InTrial);
