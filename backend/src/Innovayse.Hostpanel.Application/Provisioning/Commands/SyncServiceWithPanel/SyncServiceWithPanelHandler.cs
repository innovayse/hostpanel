using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePackage;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.SyncServiceWithPanel;

/// <summary>
/// Reads a service's hosting account from its panel once, compares it with the service through <see cref="HostedAccountDiff"/> and
/// brings the panel in step with the platform through the operations that already exist: <see cref="ChangePackageCommand"/>,
/// <see cref="ChangePasswordCommand"/> and the suspend and unsuspend of <see cref="IHostedAccountOperations"/>. The one thing taken
/// from the panel is the IP address, recorded on the service. The platform's own record of the service never changes status here.
/// </summary>
public sealed class SyncServiceWithPanelHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the service's server from the Admin module and runs the existing commands.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the service's server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Suspends and unsuspends the account on its panel.</summary>
    private readonly IHostedAccountOperations _hostedAccounts;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<SyncServiceWithPanelHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads the service's server from the Admin module and runs the existing commands.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="hostedAccounts">Suspends and unsuspends the account on its panel.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    public SyncServiceWithPanelHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        IHostedAccountOperations hostedAccounts,
        ILogger<SyncServiceWithPanelHandler> logger)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _hostedAccounts = hostedAccounts;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The service to reconcile and whether to push its password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// What was done about each field, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PROVISIONED</c> when the
    /// service has never been provisioned, <c>SERVICE_HOSTING_ACCOUNT_REMOVED</c> when it is terminated, <c>SERVICE_NO_SERVER_ASSIGNED</c> when it
    /// has no usable server, the panel's own refusal code (<see cref="PanelErrorCodes"/>) when a panel plugin refuses to be read, or
    /// <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be read. A change that fails to apply is reported in its
    /// field, not as a failure of the sync.
    /// </returns>
    public async Task<Result<ServiceSyncDto>> HandleAsync(SyncServiceWithPanelCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result<ServiceSyncDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var read = await _bus.ReadHostedAccountAsync(_providerFactory, service, _logger, ct);
        if (read.IsFailure)
        {
            return Result<ServiceSyncDto>.FailureCode(read.ErrorCode!);
        }

        // The package belongs to the Products module, so it is asked through its query like every provisioning use case does.
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(new GetProductsByIdsQuery([service.ProductId]), ct);
        var wantedPackage = products.Value?.FirstOrDefault()?.PackageName;

        var fields = new List<ServiceSyncFieldDto>();
        foreach (var drift in HostedAccountDiff.Between(service, wantedPackage, read.Value!, cmd.PushPassword))
        {
            var outcome = drift.Kind switch
            {
                HostedAccountDriftKind.InSync => PanelSyncOutcome.InSync,
                HostedAccountDriftKind.Unsupported => PanelSyncOutcome.Unsupported,
                _ => await ApplyAsync(drift, service, wantedPackage, ct),
            };
            fields.Add(new ServiceSyncFieldDto(drift.Field, outcome, drift.PanelValue));
        }

        // The address is the one thing taken from the panel: it was recorded on the service above and is saved once here.
        if (fields.Any(f => f is { Field: PanelSyncField.IpAddress, Outcome: PanelSyncOutcome.Applied }))
        {
            await _context.SaveChangesAsync(ct);
        }

        return Result<ServiceSyncDto>.Success(new ServiceSyncDto(fields));
    }

    /// <summary>Brings one field in step through the operation that already changes it.</summary>
    /// <param name="drift">The field that differs.</param>
    /// <param name="service">The service whose account is reconciled.</param>
    /// <param name="wantedPackage">The package of the service's product.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="PanelSyncOutcome.Applied"/> when the operation did the work, otherwise <see cref="PanelSyncOutcome.Failed"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="drift"/> names a field no operation changes.</exception>
    private async Task<PanelSyncOutcome> ApplyAsync(HostedAccountDrift drift, ClientService service, string? wantedPackage, CancellationToken ct)
    {
        switch (drift.Field)
        {
            case PanelSyncField.Status:
                var status = service.Status is ServiceStatus.Suspended
                    ? await _hostedAccounts.SuspendAsync(new HostedAccountSuspension(HostedAccountTarget.Of(service), string.Empty), ct)
                    : await _hostedAccounts.UnsuspendAsync(new HostedAccountUnsuspension(HostedAccountTarget.Of(service)), ct);
                return status is HostedAccountOutcome.Updated ? PanelSyncOutcome.Applied : PanelSyncOutcome.Failed;
            case PanelSyncField.Package:
                return await DispatchAsync(new ChangePackageCommand(service.Id, wantedPackage!), drift.Field, service.Id, ct);
            case PanelSyncField.Password:
                return await DispatchAsync(new ChangePasswordCommand(service.Id, service.Password!), drift.Field, service.Id, ct);
            case PanelSyncField.IpAddress:
                service.RecordAssignedIp(drift.PanelValue);
                return PanelSyncOutcome.Applied;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift), drift.Field, "No operation changes this field.");
        }
    }

    /// <summary>Runs an existing command and maps its answer onto the field's outcome.</summary>
    /// <param name="command">The command that changes the field on the panel.</param>
    /// <param name="field">The field the command changes, for the log.</param>
    /// <param name="serviceId">The service the command is for, for the log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="PanelSyncOutcome.Applied"/> when the command succeeded, otherwise <see cref="PanelSyncOutcome.Failed"/>.</returns>
    private async Task<PanelSyncOutcome> DispatchAsync(object command, PanelSyncField field, int serviceId, CancellationToken ct)
    {
        try
        {
            var result = await _bus.InvokeAsync<Result>(command, ct);
            if (result.IsFailure)
            {
                _logger.LogWarning("Syncing the {Field} of service {ServiceId} with its panel failed: {ErrorCode}.", field, serviceId, result.ErrorCode);
            }

            return result.IsSuccess ? PanelSyncOutcome.Applied : PanelSyncOutcome.Failed;
        }
        catch (ValidationException)
        {
            // The stored value is one the command would refuse (a password under the shortest the platform accepts); it is reported as
            // not applied, and nothing of the value is logged.
            _logger.LogWarning("Syncing the {Field} of service {ServiceId} with its panel was refused: the stored value is not valid.", field, serviceId);
            return PanelSyncOutcome.Failed;
        }
    }
}
