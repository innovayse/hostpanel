using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

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
    /// <summary>
    /// Seeds a client linked to a plugin account with an invoice on it, and a signed-in member the plugin
    /// maps to that account with the given rights.
    /// </summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <param name="canManagePaymentMethods">Whether the plugin lets the member manage payment methods.</param>
    /// <returns>The member and the invoice on the account.</returns>
    private async Task<(PluginMember Member, int InvoiceId)> SeedAsync(string label, bool canPurchase, bool canManagePaymentMethods)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, seeded.ClientId);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, seeded.ClientId, 10m);
        var member = await PluginMemberSeeds.SignInMemberAsync(factory, label, accountId, canPurchase, canManagePaymentMethods);

        return (member, invoiceId);
    }

    /// <summary>A member who may purchase lists and reads the linked client's invoice and reaches its payment methods.</summary>
    [Fact]
    public async Task MemberWhoMayPurchase_SeesTheLinkedClientsInvoicesAndPaymentMethodsAsync()
    {
        var (member, invoiceId) = await SeedAsync("plugin-billing-buyer", canPurchase: true, canManagePaymentMethods: false);
        using var memberLifetime = member;

        var list = await member.Client.GetAsync("/api/me/invoices");
        var read = await member.Client.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await member.Client.GetAsync("/api/me/payment-methods");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(invoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        methods.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The member's own client record is never what they act for: it has no invoices of the plugin account in it.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var (member, _) = await SeedAsync("plugin-billing-own", canPurchase: true, canManagePaymentMethods: false);
        using var memberLifetime = member;
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var ownInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, member.OwnClientId, 5m);

        var list = await member.Client.GetAsync("/api/me/invoices");
        var read = await member.Client.GetAsync($"/api/me/invoices/{ownInvoiceId}");

        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(ownInvoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(read, HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>A member who may only view is refused the invoice and payment method routes.</summary>
    [Fact]
    public async Task MemberWhoMayOnlyView_IsRefusedTheMoneyRoutesAsync()
    {
        var (member, invoiceId) = await SeedAsync("plugin-billing-viewer", canPurchase: false, canManagePaymentMethods: false);
        using var memberLifetime = member;

        var list = await member.Client.GetAsync("/api/me/invoices");
        var read = await member.Client.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await member.Client.GetAsync("/api/me/payment-methods");

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
        var (member, _) = await SeedAsync("plugin-billing-payer", canPurchase: false, canManagePaymentMethods: true);
        using var memberLifetime = member;

        var methods = await member.Client.GetAsync("/api/me/payment-methods");

        methods.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The profile belongs to the account holder alone: a plugin member never owns the client and gets no profile.</summary>
    [Fact]
    public async Task Member_GetsNoProfileOfTheLinkedClientAsync()
    {
        var (member, _) = await SeedAsync("plugin-billing-profile", canPurchase: true, canManagePaymentMethods: true);
        using var memberLifetime = member;

        var profile = await member.Client.GetAsync("/api/clients/me");

        profile.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(profile, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }
}
