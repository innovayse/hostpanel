using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A test host whose saved-method capability is one <see cref="FakeStoredCredentialPlugin"/> served for the module
/// <see cref="Module"/>, with the seeding and reading helpers the saved-card tests share. The module is not the
/// built-in one, so the admin integrations switch of the built-in gateway never gets in the way.
/// </summary>
public sealed class StoredCredentialHost
{
    /// <summary>The gateway module the fake capability answers for.</summary>
    public const string Module = FakeStoredCredentialResolver.Module;

    /// <summary>Initializes the helper.</summary>
    /// <param name="host">The built host.</param>
    /// <param name="plugin">The scripted capability.</param>
    /// <param name="resolver">The resolver serving the capability.</param>
    private StoredCredentialHost(WebApplicationFactory<Program> host, FakeStoredCredentialPlugin plugin, FakeStoredCredentialResolver resolver)
    {
        Host = host;
        Plugin = plugin;
        Resolver = resolver;
    }

    /// <summary>Gets the host to register clients and send requests against.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>Gets the scripted capability.</summary>
    public FakeStoredCredentialPlugin Plugin { get; }

    /// <summary>Gets the resolver, to take the module offline.</summary>
    public FakeStoredCredentialResolver Resolver { get; }

    /// <summary>Builds a host serving a fresh fake capability for <see cref="Module"/>.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="interceptors">Interceptors every data context of the host is built with, to hold a request at a chosen point.</param>
    /// <returns>The host with its fake capability.</returns>
    public static StoredCredentialHost Create(WebApplicationFactory<Program> factory, params IInterceptor[] interceptors) =>
        Create(factory, null, interceptors);

    /// <summary>Builds a host serving a fresh fake capability for <see cref="Module"/> and, optionally, a fake one-off card gateway.</summary>
    /// <param name="baseHost">The host to derive from (the shared one, or one with other fakes already in place).</param>
    /// <param name="cardGateway">The one-off card gateway that refunds a payment settlement hands back; null keeps the host's own.</param>
    /// <param name="interceptors">Interceptors every data context of the host is built with, to hold a request at a chosen point.</param>
    /// <returns>The host with its fake capability.</returns>
    public static StoredCredentialHost Create(
        WebApplicationFactory<Program> baseHost, FakeCardPaymentGateway? cardGateway, params IInterceptor[] interceptors)
    {
        var plugin = new FakeStoredCredentialPlugin();
        var resolver = new FakeStoredCredentialResolver(plugin);
        var host = baseHost.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStoredCredentialPluginResolver>(resolver);
                if (cardGateway is not null)
                {
                    services.AddSingleton<ICardPaymentGateway>(cardGateway);
                }

                if (interceptors.Length > 0)
                {
                    services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(interceptors));
                }
            }));
        return new StoredCredentialHost(host, plugin, resolver);
    }

    /// <summary>Links a client to a customer account at <see cref="Module"/>.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="customerRef">The gateway's reference for the account.</param>
    /// <returns>A task that completes when the link is stored.</returns>
    public Task LinkAsync(int clientId, string customerRef) =>
        BillingTestSeeds.LinkGatewayCustomerAsync(Host, clientId, Module, customerRef);

    /// <summary>Stores a consent for a client straight into the database.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="methodRef">The method the agreement is for; null leaves it Pending.</param>
    /// <param name="module">The module the agreement is with.</param>
    /// <returns>The session id the consent was stored under.</returns>
    public async Task<string> AddConsentAsync(int clientId, string? methodRef, string module = Module)
    {
        var sessionId = $"sess_{Guid.NewGuid():N}";
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = CardChargeConsent.Create(clientId, module, sessionId, null, null, DateTimeOffset.UtcNow);
        if (methodRef is not null)
        {
            consent.Activate(methodRef, DateTimeOffset.UtcNow);
        }

        context.CardChargeConsents.Add(consent);
        await context.SaveChangesAsync(CancellationToken.None);
        return sessionId;
    }

    /// <summary>Reads a client's card agreements.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The agreements, oldest first.</returns>
    public async Task<List<CardChargeConsent>> ConsentsOfAsync(int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().Where(c => c.ClientId == clientId).OrderBy(c => c.Id).ToListAsync();
    }

    /// <summary>Reads a client's gateway accounts.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The accounts.</returns>
    public async Task<List<GatewayCustomer>> CustomersOfAsync(int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.GatewayCustomers.AsNoTracking().Where(c => c.ClientId == clientId).ToListAsync();
    }

    /// <summary>Builds a saved card with the given reference.</summary>
    /// <param name="methodRef">The gateway's reference for the card.</param>
    /// <param name="isDefault">Whether the card is the account's default.</param>
    /// <returns>The saved method.</returns>
    public static SavedMethod Card(string methodRef, bool isDefault = true) => new(methodRef, "visa", "4242", 12, 2035, isDefault);
}
