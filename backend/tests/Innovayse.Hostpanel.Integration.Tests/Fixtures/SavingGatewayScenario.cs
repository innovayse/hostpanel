using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One host wired for a payment that saves the payer's method: a <see cref="SavingStoredCredentialPlugin"/> registered
/// under <see cref="Module"/> as the payment plugin and, unless the scenario is built without the capability, as the
/// stored credential plugin too. The helpers seed a client with an invoice and walk the start and the completion of its
/// gateway payment over HTTP.
/// </summary>
public sealed class SavingGatewayScenario : IDisposable
{
    /// <summary>The wording version every start in a scenario ticks unless a test says otherwise.</summary>
    public const string ConsentTextVersion = "2026-11";

    /// <summary>The shared API + PostgreSQL host the scenario builds on.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>Creates the scenario and its host.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="storedCredentials">Whether the module also offers stored credentials; <see langword="false"/> makes it a plain gateway.</param>
    public SavingGatewayScenario(IntegrationTestFactory factory, bool storedCredentials = true)
    {
        _factory = factory;
        Plugin = new SavingStoredCredentialPlugin();
        var payments = new FakePaymentPluginResolver();
        payments.Register(Module, Plugin);
        var stored = new FakeStoredCredentialPluginResolver();
        if (storedCredentials)
        {
            stored.Register(Module, Plugin);
        }

        Host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentPluginResolver>(payments);
                services.AddSingleton<IStoredCredentialPluginResolver>(stored);
            }));
    }

    /// <summary>Gets the payment plugin id the gateway is registered under. Unique per scenario, because the tests share one database.</summary>
    public string Module { get; } = $"saving-{Guid.NewGuid():N}";

    /// <summary>Gets the gateway the host serves, to read what the host asked of it and to script what it answers.</summary>
    public SavingStoredCredentialPlugin Plugin { get; }

    /// <summary>Gets the test host whose payment plugin resolvers serve <see cref="Plugin"/>.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>Registers a client on the host and raises an unpaid invoice for them.</summary>
    /// <param name="label">Distinguishes the seeded account between callers in the same test.</param>
    /// <param name="amount">The invoice total in the base currency.</param>
    /// <returns>The signed-in client and the invoice's id.</returns>
    public async Task<(SignedInClient Client, int InvoiceId)> SeedClientWithInvoiceAsync(string label, decimal amount = 10m)
    {
        var signedIn = await BillingTestSeeds.RegisterClientAsync(_factory, Host, label);
        var admin = await BillingTestSeeds.AdminClientAsync(_factory, Host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, signedIn.ClientId, amount);
        return (signedIn, invoiceId);
    }

    /// <summary>Starts the gateway payment of an invoice as the invoice's own client.</summary>
    /// <param name="caller">The signed-in client.</param>
    /// <param name="invoiceId">The invoice to pay.</param>
    /// <param name="savePaymentMethod">Whether the payer asks to keep the method they pay with.</param>
    /// <param name="consentTextVersion">The wording version ticked, or <see langword="null"/> to send none.</param>
    /// <returns>The response.</returns>
    public Task<HttpResponseMessage> StartAsync(
        HttpClient caller, int invoiceId, bool savePaymentMethod, string? consentTextVersion = ConsentTextVersion) =>
        caller.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl, savePaymentMethod, consentTextVersion });

    /// <summary>Completes the gateway payment of an invoice as the invoice's own client.</summary>
    /// <param name="caller">The signed-in client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> CompleteAsync(HttpClient caller, int invoiceId) =>
        caller.PostAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", content: null);

    /// <summary>Reads an invoice.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice as stored.</returns>
    public async Task<Invoice> InvoiceAsync(int invoiceId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Reads the consents a client holds at the scenario's module, oldest first.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The consents as stored.</returns>
    public async Task<IReadOnlyList<CardChargeConsent>> ConsentsOfAsync(int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking()
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == Module)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    /// <summary>Counts the gateway customers the host holds for a client at the scenario's module.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>How many customer rows there are.</returns>
    public async Task<int> CustomerCountAsync(int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.GatewayCustomers.AsNoTracking().CountAsync(c => c.ClientId == clientId && c.GatewayModuleId == Module);
    }

    /// <summary>Revokes a consent directly on the entity, the way the client's revoke ends up.</summary>
    /// <param name="consentId">The consent.</param>
    /// <returns>A task that completes once the revocation is saved.</returns>
    public async Task RevokeConsentAsync(int consentId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.SingleAsync(c => c.Id == consentId, CancellationToken.None);
        consent.Revoke(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Makes a consent the client's default, the way the client's choice ends up on the entity.</summary>
    /// <param name="consentId">The consent.</param>
    /// <returns>A task that completes once the choice is saved.</returns>
    public async Task MakeDefaultAsync(int consentId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.SingleAsync(c => c.Id == consentId, CancellationToken.None);
        var siblings = await context.CardChargeConsents
            .Where(c => c.ClientId == consent.ClientId && c.GatewayModuleId == consent.GatewayModuleId)
            .ToListAsync(CancellationToken.None);
        consent.SetDefault(siblings);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Binds a method to a consent directly on the entity, as an earlier completion would have.</summary>
    /// <param name="consentId">The consent.</param>
    /// <param name="methodRef">The method the consent covers.</param>
    /// <returns>A task that completes once the method is saved.</returns>
    public async Task AttachMethodAsync(int consentId, string methodRef)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.SingleAsync(c => c.Id == consentId, CancellationToken.None);
        consent.AttachMethod(methodRef);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Disposes the test host, so its background jobs stop and the memory is given back before the next test starts.</summary>
    public void Dispose()
    {
        Host.Dispose();
    }
}
