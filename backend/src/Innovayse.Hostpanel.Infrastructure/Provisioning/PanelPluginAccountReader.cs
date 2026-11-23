using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Infrastructure.Monitoring;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// Reads the disk and bandwidth usage of every account on a server from the panel plugin that serves its module: lists the accounts
/// page by page and asks for the usage of each. Used only by the disk usage report.
/// </summary>
public sealed class PanelPluginAccountReader
{
    /// <summary>
    /// The most pages one listing may take, the same ceiling the SDK's own contract check uses, so a plugin that never stops
    /// answering a next page cannot hold the report forever.
    /// </summary>
    private const int MaxPages = 1000;

    /// <summary>Bytes in a megabyte, the unit the report keeps.</summary>
    private const long BytesPerMegabyte = 1024L * 1024L;

    /// <summary>The service id given for an account found by listing the panel rather than by a service.</summary>
    private const int NoServiceId = 0;

    /// <summary>Activates the plugin and runs each call.</summary>
    private readonly PanelPluginCaller _caller;

    /// <summary>Initializes a new instance of the <see cref="PanelPluginAccountReader"/> class.</summary>
    /// <param name="caller">Activates the plugin and runs each call.</param>
    public PanelPluginAccountReader(PanelPluginCaller caller)
    {
        _caller = caller;
    }

    /// <summary>Lists the server's accounts and reads each one's usage.</summary>
    /// <param name="pluginId">The plugin that serves the server's module.</param>
    /// <param name="server">The server's connection as the plugin receives it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The usage of each account the panel lists and can measure; an account it cannot find or measure is left out.</returns>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin cannot list accounts or report usage, with its refusal code
    /// when it refuses, and with <c>PANEL_REFUSED</c> when the listing does not end within <see cref="MaxPages"/> pages.
    /// </exception>
    public async Task<IReadOnlyList<HostedAccountUsage>> ReadAsync(string pluginId, PanelServerConnection server, CancellationToken ct)
    {
        var usages = new List<HostedAccountUsage>();
        string? cursor = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var listing = await _caller.RunAsync<IProvisioningPanelPlugin, PanelAccountListResult>(
                pluginId,
                nameof(IProvisioningPanelPlugin.ListAccountsAsync),
                (plugin, token) => plugin.ListAccountsAsync(new PanelListAccountsContext(server, cursor), token),
                ct);
            listing.Outcome.EnsureDone(listing.Reason);

            foreach (var account in listing.Accounts)
            {
                var usage = await ReadAccountAsync(pluginId, server, account, ct);
                if (usage is not null)
                {
                    usages.Add(usage);
                }
            }

            if (listing.NextCursor is null)
            {
                return usages;
            }

            cursor = listing.NextCursor;
        }

        throw new DomainRuleException(HostpanelErrorCodes.PANEL_REFUSED, "The panel plugin kept listing accounts past the page limit.");
    }

    /// <summary>Reads one account's usage.</summary>
    /// <param name="pluginId">The plugin that serves the server's module.</param>
    /// <param name="server">The server's connection as the plugin receives it.</param>
    /// <param name="account">The account the panel listed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The usage in megabytes (a limit the panel does not report is zero, the report's unlimited marker), or <see langword="null"/> when the account has no domain or is gone.</returns>
    private async Task<HostedAccountUsage?> ReadAccountAsync(string pluginId, PanelServerConnection server, PanelAccount account, CancellationToken ct)
    {
        if (account.Domain is null)
        {
            return null;
        }

        // The panel lists every account on the server, including ones that belong to no service of this platform, so the
        // platform has no service id to give; the account is named by its reference alone.
        var context = new PanelAccountContext(server, new PanelService(NoServiceId, account.ProvisioningRef));
        var result = await _caller.RunAsync<IProvisioningPanelPlugin, PanelUsageResult>(
            pluginId, nameof(IProvisioningPanelPlugin.GetUsageAsync), (plugin, token) => plugin.GetUsageAsync(context, token), ct);

        // An account that vanished between the listing and the read is skipped, not a failure of the whole server.
        if (result.Outcome is PanelOperationOutcome.NotFound)
        {
            return null;
        }

        result.Outcome.EnsureDone(result.Reason);
        return result.Usage is { } usage
            ? new HostedAccountUsage(
                account.ProvisioningRef,
                account.Domain,
                ToMegabytes(usage.DiskUsedBytes),
                ToMegabytes(usage.DiskLimitBytes),
                ToMegabytes(usage.BandwidthUsedBytes),
                ToMegabytes(usage.BandwidthLimitBytes))
            : null;
    }

    /// <summary>Converts a byte figure to megabytes.</summary>
    /// <param name="bytes">The figure, or <see langword="null"/> when the panel does not report it.</param>
    /// <returns>The figure in megabytes; zero when it is not reported, which the report shows as unlimited for a limit.</returns>
    private static long ToMegabytes(long? bytes)
    {
        return (bytes ?? 0) / BytesPerMegabyte;
    }
}
