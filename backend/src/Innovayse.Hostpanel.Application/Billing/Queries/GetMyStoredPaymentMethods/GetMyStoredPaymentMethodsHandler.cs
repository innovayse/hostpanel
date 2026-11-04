using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyStoredPaymentMethods;

/// <summary>
/// Handles <see cref="GetMyStoredPaymentMethodsQuery"/>: lists the caller's active charge consents and reads the
/// details of each method live from its gateway module.
/// </summary>
/// <remarks>
/// A module that is disabled, has no stored credential capability, throws, or no longer holds the method never fails
/// the list: its consents are returned with <see cref="StoredPaymentMethodDto.DetailsAvailable"/> false. The gateway is
/// asked once per module, not once per consent. Which method is the default is the host's own record on the consent
/// (the one it charges first), not what the gateway reports, and it is reported even when the details are unavailable.
/// </remarks>
public sealed class GetMyStoredPaymentMethodsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<GetMyStoredPaymentMethodsHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="logger">Structured logger.</param>
    public GetMyStoredPaymentMethodsHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        ILogger<GetMyStoredPaymentMethodsHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _logger = logger;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved methods, oldest consent first; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record.</returns>
    public async Task<Result<IReadOnlyList<StoredPaymentMethodDto>>> HandleAsync(
        GetMyStoredPaymentMethodsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<IReadOnlyList<StoredPaymentMethodDto>>.FailureCode(clientId.ErrorCode!);
        }

        // IsActive is a computed property EF cannot translate, so the same rule (a method attached, not revoked) is spelled out.
        var consents = await _context.CardChargeConsents
            .AsNoTracking()
            .Where(c => c.ClientId == clientId.Value && c.MethodRef != null && c.RevokedAt == null)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.GatewayModuleId, c.MethodRef, c.IsDefault })
            .ToListAsync(ct);

        var customers = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == clientId.Value)
            .Select(c => new { c.GatewayModuleId, c.CustomerRef })
            .ToListAsync(ct);
        var customerByModule = customers
            .GroupBy(c => c.GatewayModuleId)
            .ToDictionary(g => g.Key, g => g.First().CustomerRef);

        var live = new Dictionary<string, IReadOnlyList<SavedMethod>?>();
        var items = new List<StoredPaymentMethodDto>(consents.Count);
        foreach (var consent in consents)
        {
            if (!live.TryGetValue(consent.GatewayModuleId, out var methods))
            {
                methods = await ReadMethodsAsync(
                    consent.GatewayModuleId, customerByModule.GetValueOrDefault(consent.GatewayModuleId), clientId.Value, ct);
                live[consent.GatewayModuleId] = methods;
            }

            var method = methods?.FirstOrDefault(m => string.Equals(m.MethodRef, consent.MethodRef, StringComparison.Ordinal));
            items.Add(method is null
                ? new StoredPaymentMethodDto(consent.Id, consent.GatewayModuleId, false, null, null, 0, 0, consent.IsDefault)
                : new StoredPaymentMethodDto(
                    consent.Id, consent.GatewayModuleId, true, method.Brand, method.Last4, method.ExpiryMonth, method.ExpiryYear, consent.IsDefault));
        }

        return Result.Success<IReadOnlyList<StoredPaymentMethodDto>>(items);
    }

    /// <summary>Reads the customer's methods from one gateway module, live.</summary>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="customerRef">The client's customer at that gateway, or <see langword="null"/> when none is recorded.</param>
    /// <param name="clientId">The client, for the log only.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The methods, or <see langword="null"/> when the module cannot say: unavailable, no customer, or a failure (logged, not thrown).</returns>
    private async Task<IReadOnlyList<SavedMethod>?> ReadMethodsAsync(string module, string? customerRef, int clientId, CancellationToken ct)
    {
        if (customerRef is null)
        {
            return null;
        }

        try
        {
            var plugin = await _plugins.ResolveAsync(module, ct);
            return plugin is null ? null : await plugin.ListMethodsAsync(customerRef, ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogWarning(ex, "Payment plugin {Module} could not list the stored methods of client {ClientId}.", module, clientId);
            return null;
        }
    }
}
