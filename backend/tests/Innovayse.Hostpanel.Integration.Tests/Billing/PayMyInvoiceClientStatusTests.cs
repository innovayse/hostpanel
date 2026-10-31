using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/pay</c> for a client whose account status forbids new card
/// payments: a suspended or closed account is refused with 403 and the gateway is never charged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayMyInvoiceClientStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved payment method the paying client owns in these tests.</summary>
    private const string SavedMethodId = "pm_status_1";

    /// <summary>A suspended or closed account cannot pay with a card; nothing is charged and the invoice stays unpaid.</summary>
    /// <param name="status">The status the account is in.</param>
    /// <param name="expectedCode">The code the refusal carries.</param>
    [Theory]
    [InlineData(ClientStatus.Suspended, HostpanelErrorCodes.CLIENT_SUSPENDED)]
    [InlineData(ClientStatus.Closed, HostpanelErrorCodes.CLIENT_CLOSED)]
    public async Task Pay_BySuspendedOrClosedAccount_IsRefusedAndNothingIsChargedAsync(ClientStatus status, string expectedCode)
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            IsConfigured = true,
            SavedPaymentMethods = [new CardPaymentMethodDto(SavedMethodId, SavedPaymentMethodType.Card, "visa", "4242", 12, 2035, true)],
        };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, $"pay-status-{status}");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_st_{Guid.NewGuid():N}"[..24]);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await ClientStatusSeeds.SetAsync(factory, clientId, status);

        var response = await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/pay", new { paymentMethodId = SavedMethodId, attemptId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, expectedCode);
        cardGateway.LastChargeInstruction.Should().BeNull();
        var fetched = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/billing/{invoiceId}");
        fetched.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }
}
