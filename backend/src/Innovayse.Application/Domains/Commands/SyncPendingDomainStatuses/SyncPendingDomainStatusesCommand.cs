namespace Innovayse.Application.Domains.Commands.SyncPendingDomainStatuses;

/// <summary>
/// Scheduled command that polls the registrar for all domains that are still in
/// <c>PendingRegistration</c> or <c>PendingTransfer</c> status and activates any that are now live.
/// </summary>
public record SyncPendingDomainStatusesCommand;
