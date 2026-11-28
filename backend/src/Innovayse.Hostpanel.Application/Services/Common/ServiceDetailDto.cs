using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>Detailed service DTO for admin detail views, including all editable and read-only fields.</summary>
/// <param name="Id">Primary key of the client service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client; every service has one, since its client cannot be deleted.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Name of the ordered product; every service has one, since its product cannot be deleted.</param>
/// <param name="Domain">Linked domain name, or <see langword="null"/> if no domain is linked.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="AllowedStatuses">The statuses staff may move the service to, the current one included; the domain's status table, so a screen offers only moves that are accepted.</param>
/// <param name="CreatedAt">UTC timestamp when the service was ordered.</param>
/// <param name="TerminatedAt">UTC timestamp when the service was terminated.</param>
/// <param name="AdminNotes">Internal admin notes.</param>
/// <param name="ServerId">FK to the provisioning server, or <see langword="null"/> if not assigned.</param>
/// <param name="Server">The assigned server, or <see langword="null"/> if none is assigned or it cannot be resolved.</param>
/// <param name="Account">The hosting account: dedicated IP, credentials and provisioning reference.</param>
/// <param name="Billing">Quantity, prices, payment details, cycle and renewal.</param>
/// <param name="Automation">The automatic suspension and termination settings.</param>
/// <param name="TrialStatus">Where the service is in its free trial, or <see langword="null"/> when it is not a trial.</param>
/// <param name="TrialEndsAt">When the free trial ends, or <see langword="null"/> when it is not a trial.</param>
/// <param name="TrialDaysLeft">Whole days left of an active trial, rounded up and never negative; <see langword="null"/> unless the trial is active.</param>
public sealed record ServiceDetailDto(
    int Id,
    int ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string? Domain,
    ServiceStatus Status,
    IReadOnlyList<ServiceStatus> AllowedStatuses,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TerminatedAt,
    string? AdminNotes,
    int? ServerId,
    ServiceServerDto? Server,
    ServiceAccountDto Account,
    ServiceBillingTermsDto Billing,
    ServiceAutomationDto Automation,
    TrialStatus? TrialStatus,
    DateTimeOffset? TrialEndsAt,
    int? TrialDaysLeft);
