using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

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
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethodId));
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "pay-cc-disabled");
        await gateway.LinkAsync(clientId, $"cus_cc_{Guid.NewGuid():N}"[..24]);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, gateway.Host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await ClientSettingsSeeds.SetAsync(factory, clientId, disableCcProcessing: true);

        var response = await owner.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/pay", new { gatewayModuleId = StoredCredentialHost.Module, paymentMethodId = SavedMethodId, attemptId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED);
        gateway.Plugin.Charges.Should().BeEmpty();
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/billing/{invoiceId}");
        fetched.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }
}
