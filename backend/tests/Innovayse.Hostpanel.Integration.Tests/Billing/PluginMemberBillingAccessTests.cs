using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/invoices</c>, <c>GET /api/me/payment-methods</c> and <c>GET /api/clients/me</c> for a member an
/// identity plugin vouches for: they act for the client linked to the plugin account and never for a client
/// record of their own, the plugin's purchase and payment method rights decide the invoice and payment method
/// routes, and the profile, which belongs to the account holder alone, is refused with
/// <c>CLIENT_PROFILE_NOT_FOUND</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMemberBillingAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The system name the stub plugin links its accounts under.</summary>
    private const string Provider = "crm";

    /// <summary>Builds a host whose identity plugin is <paramref name="plugin"/>.</summary>
    /// <param name="plugin">The enabled plugin.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(IClientResolver plugin) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));

    /// <summary>
    /// Seeds a client linked to a plugin account with an invoice on it, and a signed-in member the plugin
    /// maps to that account with the given rights.
    /// </summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <param name="canManagePaymentMethods">Whether the plugin lets the member manage payment methods.</param>
    /// <returns>The host to call, the member as a caller, the member's own client id and the invoice on the account.</returns>
    private async Task<(WebApplicationFactory<Program> Host, HttpClient Member, int OwnClientId, int InvoiceId)> SeedAsync(
        string label, bool canPurchase, bool canManagePaymentMethods)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var accountId = $"acct-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = await db.Clients.FirstAsync(c => c.Id == seeded.ClientId);
            client.SetExternalAccount(Provider, accountId);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, seeded.ClientId, 10m);
        var member = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            member.UserId,
            new ClientAccess(new ExternalAccount(Provider, accountId, null, null), canPurchase, canManagePaymentMethods));
        var host = BuildHost(plugin);
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = member.Client.DefaultRequestHeaders.Authorization;

        return (host, http, member.ClientId, invoiceId);
    }

    /// <summary>A member who may purchase lists and reads the linked client's invoice and reaches its payment methods.</summary>
    [Fact]
    public async Task MemberWhoMayPurchase_SeesTheLinkedClientsInvoicesAndPaymentMethodsAsync()
    {
        var (host, member, _, invoiceId) = await SeedAsync("plugin-billing-buyer", canPurchase: true, canManagePaymentMethods: false);
        using var hostLifetime = host;
        using var memberLifetime = member;

        var list = await member.GetAsync("/api/me/invoices");
        var read = await member.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await member.GetAsync("/api/me/payment-methods");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(invoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        methods.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The member's own client record is never what they act for: it has no invoices of the plugin account in it.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var (host, member, ownClientId, _) = await SeedAsync("plugin-billing-own", canPurchase: true, canManagePaymentMethods: false);
        using var hostLifetime = host;
        using var memberLifetime = member;
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var ownInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownClientId, 5m);

        var list = await member.GetAsync("/api/me/invoices");
        var read = await member.GetAsync($"/api/me/invoices/{ownInvoiceId}");

        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(ownInvoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(read, HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>A member who may only view is refused the invoice and payment method routes.</summary>
    [Fact]
    public async Task MemberWhoMayOnlyView_IsRefusedTheMoneyRoutesAsync()
    {
        var (host, member, _, invoiceId) = await SeedAsync("plugin-billing-viewer", canPurchase: false, canManagePaymentMethods: false);
        using var hostLifetime = host;
        using var memberLifetime = member;

        var list = await member.GetAsync("/api/me/invoices");
        var read = await member.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await member.GetAsync("/api/me/payment-methods");

        foreach (var response in new[] { list, read, methods })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }
    }

    /// <summary>A member who only manages payment methods reaches them, through the same flag the invoices need.</summary>
    [Fact]
    public async Task MemberWhoManagesPaymentMethods_ReachesThemAsync()
    {
        var (host, member, _, _) = await SeedAsync("plugin-billing-payer", canPurchase: false, canManagePaymentMethods: true);
        using var hostLifetime = host;
        using var memberLifetime = member;

        var methods = await member.GetAsync("/api/me/payment-methods");

        methods.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The profile belongs to the account holder alone: a plugin member never owns the client and gets no profile.</summary>
    [Fact]
    public async Task Member_GetsNoProfileOfTheLinkedClientAsync()
    {
        var (host, member, _, _) = await SeedAsync("plugin-billing-profile", canPurchase: true, canManagePaymentMethods: true);
        using var hostLifetime = host;
        using var memberLifetime = member;

        var profile = await member.GetAsync("/api/clients/me");

        profile.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(profile, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }
}
