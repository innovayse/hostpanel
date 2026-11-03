using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Host;

/*
 * Visibility before capability (security.md): what a plugin may read about a client is decided from the caller's
 * permissions before anything is read, never by hiding a value once fetched. A sub-user without the invoices right never
 * makes the invoices query run, and one without the master profile right never has the sign-in address placed in the
 * profile. The rows of other modules come through IPluginHostFactsReader, so this adapter holds no data context.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The SDK's <see cref="IPluginHostApi"/> for one call, bound when it is built to the call's
/// <see cref="PluginCallBinding"/>: no method takes an id, so a plugin reads only the facts of that client, and only
/// what the caller could see in the portal.
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
internal sealed class PluginHostApi : IPluginHostApi
{
    /// <summary>The call's client and the permissions its caller holds there.</summary>
    private readonly PluginCallBinding _binding;

    /// <summary>Reads the client, service and invoice rows of the bound client.</summary>
    private readonly IPluginHostFactsReader _facts;

    /// <summary>The identity provider that holds the client's sign-in email and language.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Resolves the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes the facts of one call.</summary>
    /// <param name="binding">The call's client and the permissions its caller holds there.</param>
    /// <param name="facts">Reads the client, service and invoice rows of the bound client.</param>
    /// <param name="identity">The identity provider that holds the client's sign-in email and language.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    public PluginHostApi(
        PluginCallBinding binding, IPluginHostFactsReader facts, IIdentityProvider identity, IPayerCurrencyResolver payerCurrency)
    {
        ArgumentNullException.ThrowIfNull(binding);

        _binding = binding;
        _facts = facts;
        _identity = identity;
        _payerCurrency = payerCurrency;
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
    /// <remarks>
    /// Refused until the host grants hosting access (#758): no manifest is admitted with <c>hostAccess</c> yet, so
    /// every call is <see cref="PluginHostAccessFailure.NotAllowed"/>, the answer the contract gives a plugin that did
    /// not declare it.
    /// </remarks>
    public Task<PluginSshAccess> IssueSshAccessAsync(int serviceId, string label, CancellationToken ct)
    {
        return Task.FromException<PluginSshAccess>(new PluginHostAccessException(PluginHostAccessFailure.NotAllowed));
    }

    /// <inheritdoc/>
    /// <remarks>Refused until #758, as <see cref="IssueSshAccessAsync"/>: no key was ever issued to a plugin.</remarks>
    public Task RevokeSshAccessAsync(int serviceId, Guid keyId, CancellationToken ct)
    {
        return Task.FromException(new PluginHostAccessException(PluginHostAccessFailure.NotAllowed));
    }
}
