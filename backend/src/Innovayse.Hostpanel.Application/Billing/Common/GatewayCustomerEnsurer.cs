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
    /// Returns the client's customer at the module, asking the plugin the first time and staging the row that records
    /// it. Nothing is saved: the caller saves the row together with the change it belongs to (the consent and the
    /// session of the payment, or of the save session), so a customer is never recorded for a payment that was not.
    /// </summary>
    /// <remarks>
    /// Two first requests at once both stage the row and one save loses to the unique key on the client and module;
    /// <see cref="SaveAsync"/> is the one place that settles it.
    /// </remarks>
    /// <param name="plugin">The stored credential capability of the module.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer, or <see langword="null"/> when the client has no record to build the customer from.</returns>
    public async Task<EnsuredGatewayCustomer?> EnsureAsync(IStoredCredentialPlugin plugin, int clientId, string module, CancellationToken ct)
    {
        var existing = await FindRefAsync(clientId, module, ct);
        if (existing is not null)
        {
            return new EnsuredGatewayCustomer(clientId, module, existing, null);
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
        return new EnsuredGatewayCustomer(clientId, module, created.CustomerRef, row);
    }

    /// <summary>
    /// Saves what the caller staged together with the customer row <see cref="EnsureAsync"/> staged, in one save. When
    /// the save loses because another request recorded the same customer first, only this call's row is dropped and the
    /// rest of the caller's change is saved once more: the plugin answers the same customer for the same client
    /// reference, so the row that won holds the reference this request would have stored.
    /// </summary>
    /// <param name="ensured">The customer the caller got from <see cref="EnsureAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the caller's change is saved.</returns>
    /// <exception cref="ConcurrencyConflictException">
    /// The save lost for a reason other than the customer: nobody recorded the customer, so the conflict is the caller's.
    /// </exception>
    public async Task SaveAsync(EnsuredGatewayCustomer ensured, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ensured);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException) when (ensured.Created is { } staged)
        {
            // A conflict with the customer's row means the winner's row exists now, so a missing row says the save
            // lost on something else (the invoice moved) and that is not this method's to retry.
            _context.Detach(staged);
            if (await FindRefAsync(ensured.ClientId, ensured.GatewayModuleId, ct) is null)
            {
                throw;
            }

            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>Reads the customer reference the host holds for a client at a module.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reference, or <see langword="null"/> when none is recorded yet.</returns>
    private Task<string?> FindRefAsync(int clientId, string module, CancellationToken ct) =>
        CustomerRefs(clientId, module).FirstOrDefaultAsync(ct);

    /// <summary>The customer references recorded for a client at a module, read without tracking.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <returns>The query over the recorded references.</returns>
    private IQueryable<string> CustomerRefs(int clientId, string module) =>
        _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == module)
            .Select(c => c.CustomerRef);

    /// <summary>The host's stable reference for a client, opaque to the plugin.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The reference the plugin is asked about, the same on every call for the client.</returns>
    private static string ClientReference(int clientId) => string.Create(CultureInfo.InvariantCulture, $"client-{clientId}");
}
