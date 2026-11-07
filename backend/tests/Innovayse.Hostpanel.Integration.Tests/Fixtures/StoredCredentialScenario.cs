using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Time;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One host wired for the stored credential capability: the demo payment plugin, which picks a charge outcome by the
/// last two digits of the amount in minor units, is registered under <see cref="Module"/> in a
/// <see cref="FakeStoredCredentialPluginResolver"/>, and the helpers seed the rows and dispatch the messages the
/// stored charge tests need.
/// </summary>
public sealed class StoredCredentialScenario : IDisposable
{
    /// <summary>The stored method every seeded consent covers.</summary>
    public const string MethodRef = "demo-pm-1";

    /// <summary>The shared API + PostgreSQL host the scenario builds on.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>Creates the scenario and its host.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="emailSender">
    /// A fake mail relay to host in place of the real one, so a test can read the mail the charge outcome sends;
    /// <see langword="null"/> leaves the host's relay as it is.
    /// </param>
    /// <param name="saveInterceptor">
    /// An interceptor every data context of the host is built with, so a test can run a competing write at a chosen
    /// save; <see langword="null"/> adds none.
    /// </param>
    public StoredCredentialScenario(
        IntegrationTestFactory factory, FakeEmailSender? emailSender = null, IInterceptor? saveInterceptor = null)
    {
        _factory = factory;
        State = new DemoStoredCredentialState(new SystemClock(TimeProvider.System));
        Plugin = new DemoPaymentPlugin("840", ["840"], State);
        Resolver = new FakeStoredCredentialPluginResolver();
        Resolver.Register(Module, Plugin);
        Host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStoredCredentialPluginResolver>(Resolver);
                if (saveInterceptor is not null)
                {
                    services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(saveInterceptor));
                }

                if (emailSender is not null)
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton<IEmailSender>(emailSender);
                }
            }));
    }

    /// <summary>
    /// Gets the payment plugin id the demo capability is registered under. Unique per scenario, because the tests share one
    /// database and the reconciliation pass looks at every stale claim in it: a claim of another scenario must resolve to
    /// no capability here instead of being looked up in this scenario's book and read as never charged.
    /// </summary>
    public string Module { get; } = $"stored-{Guid.NewGuid():N}";

    /// <summary>Gets the book of the demo gateway's methods and charges, to count what actually took effect.</summary>
    public DemoStoredCredentialState State { get; }

    /// <summary>Gets the demo plugin registered as the capability.</summary>
    public IStoredCredentialPlugin Plugin { get; }

    /// <summary>Gets the resolver the host serves capabilities through; a test unregisters a module to switch it off.</summary>
    public FakeStoredCredentialPluginResolver Resolver { get; }

    /// <summary>Gets the test host whose stored credential resolver is <see cref="Resolver"/>.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>Seeds a client row and makes sure the currency its invoices are issued in exists.</summary>
    /// <returns>The new client's id.</returns>
    public async Task<int> SeedClientAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(Host, "USD", "840", 1m);
        return await BillingTestSeeds.SeedClientAsync(Host, "USD");
    }

    /// <summary>Seeds a client backed by a real sign-in account, so mail addressed to the client resolves an address.</summary>
    /// <returns>The new client's id and the address of the account.</returns>
    public async Task<(int ClientId, string Email)> SeedMailableClientAsync()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(Host, "USD", "840", 1m);
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (client, email) = await EventHandlerTestHost.SeedIdentityClientAsync(scope.ServiceProvider, context, "stored-charge");
        client.SetCurrency("USD");
        await context.SaveChangesAsync(CancellationToken.None);
        return (client.Id, email);
    }

    /// <summary>Records an active consent for <paramref name="methodRef"/> and the gateway customer it belongs to.</summary>
    /// <param name="clientId">The client who consented.</param>
    /// <param name="methodRef">The stored method the consent covers; <see cref="MethodRef"/> unless a test needs a second method.</param>
    /// <returns>The consent's id.</returns>
    public async Task<int> SeedConsentAsync(int clientId, string methodRef = MethodRef)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        if (!await context.GatewayCustomers.AnyAsync(c => c.ClientId == clientId && c.GatewayModuleId == Module, CancellationToken.None))
        {
            context.GatewayCustomers.Add(GatewayCustomer.Create(clientId, Module, $"demo-cus-client-{clientId}", now));
        }

        var consent = CardChargeConsent.Grant(clientId, Module, $"session-{Guid.NewGuid():N}", now);
        consent.AttachMethod(methodRef);
        context.CardChargeConsents.Add(consent);
        await context.SaveChangesAsync(CancellationToken.None);
        return consent.Id;
    }

    /// <summary>Makes a seeded consent the client's default, the way the client's choice ends up on the entity.</summary>
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

    /// <summary>Reads one consent.</summary>
    /// <param name="consentId">The consent.</param>
    /// <returns>The consent.</returns>
    public async Task<CardChargeConsent> ConsentAsync(int consentId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.Id == consentId, CancellationToken.None);
    }

    /// <summary>
    /// Writes a claim for an invoice that the gateway took money for but the invoice could not record, ended as held for
    /// an operator.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="clientId">The client whose method was charged.</param>
    /// <returns>A task that completes once the claim is saved.</returns>
    public async Task SeedHeldClaimAsync(int invoiceId, int clientId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var claim = InvoicePaymentClaim.Open(invoiceId, clientId, Module, MethodRef, $"held-{Guid.NewGuid():N}", 1000, "USD", now);
        claim.MarkHeld("held-tx", now);
        context.InvoicePaymentClaims.Add(claim);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Moves the end of an invoice's declined and abandoned claims back in time, so the retry wait has passed.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="by">How far back to move them.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    public async Task AgeFailedClaimsAsync(int invoiceId, TimeSpan by)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = DateTimeOffset.UtcNow - by;
        await context.InvoicePaymentClaims
            .Where(c => c.InvoiceId == invoiceId && (c.Status == InvoicePaymentClaimStatus.Declined || c.Status == InvoicePaymentClaimStatus.Abandoned))
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ResolvedAt, (DateTimeOffset?)old).SetProperty(c => c.CreatedAt, old));
    }

    /// <summary>
    /// Stores a real demo card for the client at the gateway and records an active consent that covers it, so the
    /// gateway can list the method live (unlike <see cref="SeedConsentAsync"/>, whose fixed method reference the gateway
    /// does not hold).
    /// </summary>
    /// <param name="clientId">The client who consented.</param>
    /// <returns>The consent's id and the method reference the gateway holds.</returns>
    public async Task<(int ConsentId, string MethodRef)> SeedStoredMethodAsync(int clientId)
    {
        var customerRef = $"demo-cus-client-{clientId}";
        var session = await Plugin.BeginSaveMethodAsync(new(customerRef, "https://client.test/x"), CancellationToken.None);
        var saved = await Plugin.CompleteSaveMethodAsync(session.SessionId, CancellationToken.None);

        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        if (!await context.GatewayCustomers.AnyAsync(c => c.ClientId == clientId && c.GatewayModuleId == Module, CancellationToken.None))
        {
            context.GatewayCustomers.Add(GatewayCustomer.Create(clientId, Module, customerRef, now));
        }

        var consent = CardChargeConsent.Grant(clientId, Module, session.SessionId, now);
        consent.AttachMethod(saved.Method!.MethodRef);
        context.CardChargeConsents.Add(consent);
        await context.SaveChangesAsync(CancellationToken.None);
        return (consent.Id, saved.Method.MethodRef);
    }

    /// <summary>Revokes a seeded consent directly on the entity, the way the client's revoke ends up.</summary>
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

    /// <summary>Raises an unpaid invoice for a client.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="amount">
    /// The invoice total in USD. Its last two digits in minor units pick the demo charge outcome: .00 settles, .02
    /// declines, .03 asks the payer to authenticate, .04 stays pending, .05 takes the money and then times out, .06
    /// times out before taking anything.
    /// </param>
    /// <returns>The invoice's id.</returns>
    public async Task<int> SeedInvoiceAsync(int clientId, decimal amount)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", amount, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Records a part payment on an invoice, the way staff would, so it owes less than when it was claimed.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The part payment in USD.</param>
    /// <returns>A task that completes once the payment is saved.</returns>
    public async Task AddPaymentAsync(int invoiceId, decimal amount)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId, CancellationToken.None);
        context.Transactions.Add(invoice.AddPayment(
            DateTimeOffset.UtcNow, "manual", $"manual-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Dispatches a message on a fresh scope's bus.</summary>
    /// <typeparam name="TResult">The handler's result type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <returns>The handler's result.</returns>
    public async Task<TResult> InvokeAsync<TResult>(object message)
    {
        using var scope = Host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<TResult>(message);
    }

    /// <summary>
    /// Dispatches a message and waits until everything it set off has been handled, the mail its domain events
    /// queue included.
    /// </summary>
    /// <typeparam name="TResult">The handler's result type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <returns>The handler's result.</returns>
    public async Task<TResult> InvokeAndSettleAsync<TResult>(object message)
    {
        TResult result = default!;
        await Host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async bus => result = await bus.InvokeAsync<TResult>(message)));
        return result;
    }

    /// <summary>Reads the claims written for an invoice, oldest first.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The claims.</returns>
    public async Task<IReadOnlyList<InvoicePaymentClaim>> ClaimsOfAsync(int invoiceId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoicePaymentClaims.AsNoTracking()
            .Where(c => c.InvoiceId == invoiceId)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    /// <summary>Reads an invoice with its payments.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice.</returns>
    public async Task<Invoice> InvoiceAsync(int invoiceId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Moves a claim's creation back in time, so the reconciliation pass sees it as stale.</summary>
    /// <param name="claimId">The claim.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    public async Task AgeClaimAsync(int claimId)
    {
        using var scope = Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = DateTimeOffset.UtcNow - InvoicePaymentClaim.StaleAfter - TimeSpan.FromMinutes(1);
        await context.InvoicePaymentClaims.Where(c => c.Id == claimId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.CreatedAt, old).SetProperty(c => c.CheckedAt, (DateTimeOffset?)null));
    }

    /// <summary>Disposes the test host, so its background jobs stop and the memory is given back before the next test starts.</summary>
    public void Dispose()
    {
        Host.Dispose();
    }
}
