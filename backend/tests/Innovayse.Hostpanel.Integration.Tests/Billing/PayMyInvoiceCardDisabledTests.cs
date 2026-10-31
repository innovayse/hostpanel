using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/pay</c> for a client whose card processing is disabled: the saved
/// card is never charged and the invoice stays unpaid.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayMyInvoiceCardDisabledTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved payment method the paying client owns in these tests.</summary>
    private const string SavedMethodId = "pm_cc_disabled_1";

    /// <summary>An account with card processing disabled is refused with 403 and nothing is charged.</summary>
    [Fact]
    public async Task Pay_ByAccountWithCardProcessingDisabled_IsRefusedAndNothingIsChargedAsync()
    {
        var cardGateway = new FakeCardPaymentGateway
        {
            IsConfigured = true,
            SavedPaymentMethods = [new CardPaymentMethodDto(SavedMethodId, SavedPaymentMethodType.Card, "visa", "4242", 12, 2035, true)],
        };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "pay-cc-disabled");
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_cc_{Guid.NewGuid():N}"[..24]);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await ClientSettingsSeeds.SetAsync(factory, clientId, disableCcProcessing: true);

        var response = await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/pay", new { paymentMethodId = SavedMethodId, attemptId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED);
        cardGateway.LastChargeInstruction.Should().BeNull();
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/billing/{invoiceId}");
        fetched.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }
}
