using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/invoices</c>, <c>GET /api/me/invoices/{id}</c> and <c>GET /api/me/stored-payment-methods</c> for
/// the users of an account: the owner and a user linked with the invoices permission see the
/// account's invoices; a user without the permission is refused; a user of another account gets an
/// empty list and 404 for an id that is not theirs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserInvoiceAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner lists and reads their own invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerSeesTheInvoiceAsync()
    {
        var (owner, invoiceId) = await OwnerWithInvoiceAsync("linked-invoice-owner");

        var list = await owner.Client.GetAsync("/api/me/invoices");
        var read = await owner.Client.GetAsync($"/api/me/invoices/{invoiceId}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(invoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked with the invoices permission lists and reads the account's invoice and its payment methods.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserSeesTheInvoiceAsync()
    {
        var (owner, invoiceId) = await OwnerWithInvoiceAsync("linked-invoice-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-invoice-permitted", owner.ClientId, ClientPermission.ViewPayInvoices);

        var list = await user.GetAsync("/api/me/invoices");
        var read = await user.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await user.GetAsync("/api/me/stored-payment-methods");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(invoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        methods.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A user linked without the invoices permission is refused every invoice route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, invoiceId) = await OwnerWithInvoiceAsync("linked-invoice-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-invoice-denied", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/invoices");
        var read = await user.GetAsync($"/api/me/invoices/{invoiceId}");
        var methods = await user.GetAsync("/api/me/stored-payment-methods");

        foreach (var response in new[] { list, read, methods })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }
    }

    /// <summary>
    /// A user permitted on another account never sees this account's invoice: the list is theirs
    /// alone and the id answers <c>INVOICE_NOT_FOUND</c>, like an id naming no invoice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountAnswersNotFoundAsync()
    {
        var (_, invoiceId) = await OwnerWithInvoiceAsync("linked-invoice-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-invoice-foreign", otherClientId, ClientPermission.ViewPayInvoices);

        var list = await user.GetAsync("/api/me/invoices");
        var read = await user.GetAsync($"/api/me/invoices/{invoiceId}");

        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(invoiceId);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(read, HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Registers an account owner and raises an invoice on the account as staff.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The signed-in owner and the invoice id.</returns>
    private async Task<(SignedInClient Owner, int InvoiceId)> OwnerWithInvoiceAsync(string label)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, label);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, owner.ClientId, 10m);
        return (owner, invoiceId);
    }
}
