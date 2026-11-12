using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One host wired for a payment that saves the payer's method: a <see cref="SavingStoredCredentialPlugin"/> registered
/// under <see cref="Module"/> as the payment plugin and, unless the scenario is built without the capability, as the
/// stored credential plugin too (without it the host sees a plain gateway). The helpers seed a client with an invoice and walk the start and the completion of its
/// gateway payment over HTTP.
/// </summary>
public sealed class SavingGatewayScenario : IDisposable
{
    /// <summary>The wording version every start in a scenario ticks unless a test says otherwise.</summary>
    public const string ConsentTextVersion = ConsentTextVersionWire.CheckoutAgreement;

    /// <summary>The shared API + PostgreSQL host the scenario builds on.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>Creates the scenario and its host.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="storedCredentials">Whether the module also offers stored credentials; <see langword="false"/> makes it a plain gateway.</param>
    /// <param name="saveInterceptor">
    /// An interceptor every data context of the host is built with, so a test can run a competing write at a chosen save;
    /// <see langword="null"/> adds none.
    /// </param>
    public SavingGatewayScenario(
        IntegrationTestFactory factory, bool storedCredentials = true, IInterceptor? saveInterceptor = null)
    {
        _factory = factory;
        Plugin = new SavingStoredCredentialPlugin();
        var payments = new FakePaymentPluginResolver();
        payments.Register(Module, storedCredentials ? Plugin : new PaymentOnlyPlugin(Plugin));
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
                if (saveInterceptor is not null)
                {
                    services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(saveInterceptor));
                }
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
        caller.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", new { });

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

    /// <summary>Signs in a user who owns no client and is linked to a client account with the given permissions, on the scenario's host.</summary>
    /// <param name="label">Distinguishes the created address between callers in the same test.</param>
    /// <param name="clientId">The account to link to.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>The linked user's authenticated client, served by <see cref="Host"/>.</returns>
    public async Task<HttpClient> SignInLinkedUserAsync(string label, int clientId, ClientPermission permissions)
    {
        var subject = await ClientsTestSeeds.CreateUnlinkedSubjectAsync(_factory, label);
        var linked = Host.CreateClient();
        var token = await _factory.GetClientTokenAsync(subject.Email, ClientsTestSeeds.TestPassword);
        linked.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await ClientsTestSeeds.LinkUserAsync(_factory, linked, subject.Email, clientId, permissions);
        return linked;
    }

    /// <summary>
    /// Moves the start of an invoice's gateway session into the past, past the margin the reconciliation job leaves a payer
    /// on the payment page, so the job picks the invoice up.
    /// </summary>
    /// <param name="invoiceId">The invoice whose session was started.</param>
    /// <param name="startedAt">The moment the session is made to have started.</param>
    /// <returns>A task that completes once the new start is saved.</returns>
    public async Task BackdateSessionAsync(int invoiceId, DateTimeOffset startedAt)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.SingleAsync(i => i.Id == invoiceId, CancellationToken.None);
        invoice.SetGatewaySession(invoice.GatewayModule!, invoice.GatewayOrderId!, startedAt);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Disposes the test host, so its background jobs stop and the memory is given back before the next test starts.</summary>
    public void Dispose()
    {
        Host.Dispose();
    }
}
