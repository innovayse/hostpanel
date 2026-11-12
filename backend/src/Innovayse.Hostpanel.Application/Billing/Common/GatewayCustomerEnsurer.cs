using System.Globalization;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place the host finds or creates a client's customer at a payment gateway module that offers stored
/// credentials. The host keeps one customer per client and module in <c>GatewayCustomers</c>; the plugin is asked for it
/// only the first time. Opening a save session and registering a payment that saves its method both need the customer
/// before the gateway is asked, because every later stored charge is made against it.
/// </summary>
public sealed class GatewayCustomerEnsurer
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The Wolverine bus, for the Clients module's brief of the client.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the customer's creation is stamped from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="GatewayCustomerEnsurer"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The Wolverine bus, for the Clients module's brief of the client.</param>
    /// <param name="clock">The clock the customer's creation is stamped from.</param>
    public GatewayCustomerEnsurer(IHostpanelDbContext context, IMessageBus bus, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>
    /// Returns the client's customer reference at the module, asking the plugin and recording it the first time. Two
    /// first requests at once end with the one the database kept.
    /// </summary>
    /// <remarks>
    /// Saves at once when it records the customer, so the caller must have nothing else staged that it is not ready to
    /// save. When the save loses to another request only the customer row of this call is dropped from the context:
    /// whatever else the caller has loaded or staged stays.
    /// </remarks>
    /// <param name="plugin">The stored credential capability of the module.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer reference, or <see langword="null"/> when the client has no record to build the customer from.</returns>
    public async Task<string?> EnsureAsync(IStoredCredentialPlugin plugin, int clientId, string module, CancellationToken ct)
    {
        var existing = await FindRefAsync(clientId, module, ct);
        if (existing is not null)
        {
            return existing;
        }

        var client = await _bus.FindClientBriefAsync(clientId, withEmail: true, ct);
        if (client is null)
        {
            return null;
        }

        var created = await plugin.EnsureCustomerAsync(
            new CredentialCustomerContext(ClientReference(clientId), client.Email, client.FullName), ct);

        var row = GatewayCustomer.Create(clientId, module, created.CustomerRef, _clock.UtcNow);
        _context.GatewayCustomers.Add(row);
        try
        {
            await _context.SaveChangesAsync(ct);
            return created.CustomerRef;
        }
        catch (ConcurrencyConflictException)
        {
            // Another request recorded the customer first. The plugin answers the same customer for the same client
            // reference, so the row that won holds the reference this request would have stored.
            _context.Detach(row);
            return await FindRefAsync(clientId, module, ct);
        }
    }

    /// <summary>Reads the customer reference the host holds for a client at a module.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reference, or <see langword="null"/> when none is recorded yet.</returns>
    private Task<string?> FindRefAsync(int clientId, string module, CancellationToken ct) =>
        _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == module)
            .Select(c => c.CustomerRef)
            .FirstOrDefaultAsync(ct);

    /// <summary>The host's stable reference for a client, opaque to the plugin.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The reference the plugin is asked about, the same on every call for the client.</returns>
    private static string ClientReference(int clientId) => string.Create(CultureInfo.InvariantCulture, $"client-{clientId}");
}
