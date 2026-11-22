using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/*
 * Visibility before capability (security.md): what a plugin may read about a client is decided from the caller's
 * permissions before anything is read, never by hiding a value once fetched. A sub-user without the invoices right never
 * makes the invoices query run, and one without the master profile right never has the sign-in address placed in the
 * profile. The rows of other modules come through IPluginHostFactsReader, so this adapter holds no data context.
 *
 * The two SSH methods (#758), the hosting methods (#1294) and the service connection method (#1357) only delegate to the
 * Application's PluginSshAccessService, PluginHostingService and PluginServiceConnectionService with this call's
 * binding, and turn its refusal into the SDK's PluginHostAccessException at the plugin boundary. The order read (#1356)
 * delegates to PluginOrderReadService the same way and has no refusal to turn: the SDK says the host may ignore the
 * request and never reports what it did. No use case is dispatched from here: an adapter never calls a handler.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The SDK's <see cref="IPluginHostApi"/> for one call, bound when it is built to the call's
/// <see cref="PluginCallBinding"/>: no method takes a client id, so a plugin reads only the facts of that client, and
/// only what the caller could see in the portal. It also implements the optional <see cref="IPluginHostHostingApi"/>
/// (websites and databases on the client's hosting, #1294), <see cref="IPluginHostServiceConnectionApi"/> (the
/// server connection of one of the client's services, #1357) and <see cref="IPluginHostPaymentApi"/> (a payment plugin's
/// callback asks the host to read one gateway order, #1356).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="ListServicesAsync"/> answers empty unless the caller holds <see cref="ClientPermission.ViewProductsServices"/>.</item>
/// <item><see cref="ListInvoicesAsync"/> answers empty unless the caller holds <see cref="ClientPermission.ViewPayInvoices"/>; drafts are never shared.</item>
/// <item>
/// <see cref="GetProfileAsync"/> answers the name, currency and language to any client caller, and the <c>Email</c> only when
/// the caller holds <see cref="ClientPermission.ModifyMasterProfile"/>: the account's sign-in address is part of the master
/// profile, which a sub-user without that right never sees in the portal either.
/// </item>
/// </list>
/// Each list is the newest <see cref="PluginHostLimits.MaxListed"/> rows, and a call without a client, or whose client row
/// is gone, gets no profile and empty lists.
/// </remarks>
internal sealed class PluginHostApi : IPluginHostHostingApi, IPluginHostServiceConnectionApi, IPluginHostPaymentApi
{
    /// <summary>The call's client and the permissions its caller holds there.</summary>
    private readonly PluginCallBinding _binding;

    /// <summary>Reads the client, service and invoice rows of the bound client.</summary>
    private readonly IPluginHostFactsReader _facts;

    /// <summary>The identity provider that holds the client's sign-in email and language.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Resolves the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Issues and revokes the plugin's SSH identities for the bound client.</summary>
    private readonly PluginSshAccessService _ssh;

    /// <summary>Reads what the hosting can do, and sets up and removes the plugin's websites and databases for the bound client.</summary>
    private readonly PluginHostingService _hosting;

    /// <summary>Hands the plugin the server connection of one of the bound client's services.</summary>
    private readonly PluginServiceConnectionService _connections;

    /// <summary>Reads the gateway order a payment plugin's callback names.</summary>
    private readonly PluginOrderReadService _orders;

    /// <summary>Initializes the facts of one call.</summary>
    /// <param name="binding">The call's client and the permissions its caller holds there.</param>
    /// <param name="facts">Reads the client, service and invoice rows of the bound client.</param>
    /// <param name="identity">The identity provider that holds the client's sign-in email and language.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    /// <param name="ssh">Issues and revokes the plugin's SSH identities for the bound client.</param>
    /// <param name="hosting">Reads what the hosting can do, and sets up and removes the plugin's websites and databases.</param>
    /// <param name="connections">Hands the plugin the server connection of one of the bound client's services.</param>
    /// <param name="orders">Reads the gateway order a payment plugin's callback names.</param>
    public PluginHostApi(
        PluginCallBinding binding,
        IPluginHostFactsReader facts,
        IIdentityProvider identity,
        IPayerCurrencyResolver payerCurrency,
        PluginSshAccessService ssh,
        PluginHostingService hosting,
        PluginServiceConnectionService connections,
        PluginOrderReadService orders)
    {
        _binding = binding;
        _facts = facts;
        _identity = identity;
        _payerCurrency = payerCurrency;
        _ssh = ssh;
        _hosting = hosting;
        _connections = connections;
        _orders = orders;
    }

    /// <inheritdoc/>
    public async Task<PluginClientProfile?> GetProfileAsync(CancellationToken ct)
    {
        if (_binding.ClientId is not { } id)
        {
            return null;
        }

        var client = await _facts.FindClientAsync(id, ct);
        if (client is null)
        {
            return null;
        }

        var name = !string.IsNullOrWhiteSpace(client.CompanyName)
            ? client.CompanyName.Trim()
            : $"{client.FirstName} {client.LastName}".Trim();

        // The account is read for the language every client caller may see; its address is copied only for a caller who
        // holds the master profile right, decided from the binding and not from what the read returned.
        var account = string.IsNullOrEmpty(client.OwnerSubject)
            ? null
            : await _identity.FindBySubjectAsync(client.OwnerSubject, ct);
        var email = _binding.Permissions.HasFlag(ClientPermission.ModifyMasterProfile) ? account?.Email : null;

        var currency = await _payerCurrency.ForClientAsync(id, ct);

        return new PluginClientProfile(name, email, currency.Code, account?.Language);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginClientService>> ListServicesAsync(CancellationToken ct)
    {
        if (_binding.ClientId is not { } id || !_binding.Permissions.HasFlag(ClientPermission.ViewProductsServices))
        {
            return [];
        }

        var rows = await _facts.ListNewestServicesAsync(id, ct);
        return rows
            .Select(s => s.ToPlugin())
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginClientInvoice>> ListInvoicesAsync(CancellationToken ct)
    {
        if (_binding.ClientId is not { } id || !_binding.Permissions.HasFlag(ClientPermission.ViewPayInvoices))
        {
            return [];
        }

        var rows = await _facts.ListNewestInvoicesAsync(id, ct);
        return rows
            .Select(i => i.ToPlugin())
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<PluginSshAccess> IssueSshAccessAsync(int serviceId, string label, CancellationToken ct)
    {
        return (await _ssh.IssueAsync(_binding, serviceId, label, ct)).GrantedOrThrow();
    }

    /// <inheritdoc/>
    public async Task RevokeSshAccessAsync(int serviceId, Guid keyId, CancellationToken ct)
    {
        (await _ssh.RevokeAsync(_binding, serviceId, keyId, ct)).ThrowIfRefused();
    }

    /// <inheritdoc/>
    public async Task<PluginHostingCapabilities> GetHostingCapabilitiesAsync(int serviceId, CancellationToken ct)
    {
        return (await _hosting.GetCapabilitiesAsync(_binding, serviceId, ct)).GrantedOrThrow().ToPlugin();
    }

    /// <inheritdoc/>
    public async Task<PluginWebsite> EnsureWebsiteAsync(int serviceId, PluginWebsiteSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return (await _hosting.EnsureWebsiteAsync(_binding, serviceId, spec.ToHosted(), ct)).GrantedOrThrow().ToPlugin();
    }

    /// <inheritdoc/>
    public async Task RemoveWebsiteAsync(int serviceId, Guid websiteId, CancellationToken ct)
    {
        (await _hosting.RemoveWebsiteAsync(_binding, serviceId, websiteId, ct)).ThrowIfRefused();
    }

    /// <inheritdoc/>
    public async Task<PluginDatabase> EnsureDatabaseAsync(int serviceId, string label, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(label);
        return (await _hosting.EnsureDatabaseAsync(_binding, serviceId, label, ct)).GrantedOrThrow().ToPlugin();
    }

    /// <inheritdoc/>
    public async Task RemoveDatabaseAsync(int serviceId, Guid databaseId, CancellationToken ct)
    {
        (await _hosting.RemoveDatabaseAsync(_binding, serviceId, databaseId, ct)).ThrowIfRefused();
    }

    /// <inheritdoc/>
    public async Task<PanelAccountContext> ResolveServiceConnectionAsync(int serviceId, CancellationToken ct)
    {
        return (await _connections.ResolveAsync(_binding, serviceId, ct)).GrantedOrThrow().ToPanel();
    }

    /// <inheritdoc/>
    public async Task RequestOrderReadAsync(string gatewayOrderId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gatewayOrderId);
        await _orders.RequestAsync(_binding, gatewayOrderId, ct);
    }
}
