using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Extensions;

/// <summary>
/// The one read of a service's hosting account from its panel, shared by every use case that reconciles the platform with the panel,
/// so the account check, the server lookup and the mapping of a panel failure are written once.
/// </summary>
public static class HostedAccountReadExtensions
{
    /// <summary>Reads what the panel reports about the service's hosting account. Read only: nothing is changed on the panel.</summary>
    /// <param name="bus">The Wolverine bus the service's server is read through.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the service's server.</param>
    /// <param name="service">The service whose account is read.</param>
    /// <param name="logger">Records a refusal or a failure of the panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The state the panel reports, or <c>SERVICE_NOT_PROVISIONED</c> when the service has never been provisioned,
    /// <c>SERVICE_NOT_ACTIVE</c> when it is terminated, <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server, the panel's own
    /// refusal code (<see cref="PanelErrorCodes"/>) when a panel plugin refuses with a reason, or
    /// <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be read.
    /// </returns>
    public static async Task<Result<HostedAccountState>> ReadHostedAccountAsync(
        this IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        ClientService service,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(service);

        var account = service.RequireHostingAccount();
        if (account.IsFailure)
        {
            return Result<HostedAccountState>.FailureCode(account.ErrorCode!);
        }

        var server = await bus.FindServerAsync(service, ct);
        if (server is null)
        {
            return Result<HostedAccountState>.FailureCode(HostpanelErrorCodes.SERVICE_NO_SERVER_ASSIGNED);
        }

        return await providerFactory.ReadHostedAccountAsync(server, new AccountStateOrder(service.Id, account.Value!), logger, ct);
    }

    /// <summary>
    /// Reads what a server's panel reports about one account, for a caller that has a server and an account reference but no service
    /// to read it for yet (the admin adding an account that already exists). Read only: nothing is changed on the panel.
    /// </summary>
    /// <param name="providerFactory">Creates the provisioning provider configured for the server.</param>
    /// <param name="server">The server whose panel is asked.</param>
    /// <param name="order">The account to read; its service id is 0 when no service holds the account yet.</param>
    /// <param name="logger">Records a refusal or a failure of the panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The state the panel reports, the panel's own refusal code (<see cref="PanelErrorCodes"/>, <c>PANEL_ACCOUNT_NOT_FOUND</c> when it
    /// holds no such account and <c>PANEL_OPERATION_NOT_SUPPORTED</c> when it cannot read accounts) or
    /// <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be read.
    /// </returns>
    public static async Task<Result<HostedAccountState>> ReadHostedAccountAsync(
        this IProvisioningProviderFactory providerFactory,
        ServerConnectionDto server,
        AccountStateOrder order,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(order);

        return await PanelCall.RunAsync(
            () => providerFactory.CreateFor(server).GetAccountAsync(order, ct),
            logger, "Reading the account", order.ServiceId, server.Id, HostpanelErrorCodes.SERVICE_PANEL_SYNC_UPSTREAM_FAILED);
    }
}
