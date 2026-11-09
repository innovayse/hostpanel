using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// A draft invoice is staff work in progress: its own client reaches it through none of the
/// client-facing invoice paths, and each answers exactly like an invoice that does not exist.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DraftInvoiceClientAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Registers a client and raises a draft invoice for them.</summary>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <returns>The client's caller and the draft invoice id.</returns>
    private async Task<(HttpClient Owner, int DraftId)> SeedDraftAsync(string label)
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, label);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var draftId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m, isDraft: true);
        return (owner, draftId);
    }

    /// <summary>Asserts that a response is the same 404 an absent invoice gets, body included.</summary>
    /// <param name="response">The response to the draft invoice request.</param>
    /// <param name="absent">The response to the same request for an id that does not exist.</param>
    /// <returns>A task.</returns>
    private static async Task AssertIndistinguishableFromAbsentAsync(HttpResponseMessage response, HttpResponseMessage absent)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        absent.StatusCode.Should().Be(response.StatusCode);
        (await absent.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Reading the detail of the caller's own draft answers 404 like an unknown invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_AnOwnDraft_IsNotFoundLikeAnAbsentInvoice()
    {
        var (owner, draftId) = await SeedDraftAsync("draft-get");

        var response = await owner.GetAsync($"/api/me/invoices/{draftId}");
        var absent = await owner.GetAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}");

        await AssertIndistinguishableFromAbsentAsync(response, absent);
    }

    /// <summary>Starting a gateway payment on the caller's own draft answers 404 like an unknown invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartGatewayPayment_AnOwnDraft_IsNotFoundLikeAnAbsentInvoice()
    {
        var (owner, draftId) = await SeedDraftAsync("draft-gw-start");
        var body = new { module = "bank-gateway", returnUrl = BillingTestSeeds.AllowedReturnUrl };

        var response = await owner.PostAsJsonAsync($"/api/me/invoices/{draftId}/gateway-payment/start", body);
        var absent = await owner.PostAsJsonAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}/gateway-payment/start", body);

        await AssertIndistinguishableFromAbsentAsync(response, absent);
    }

    /// <summary>Completing a gateway payment on the caller's own draft answers 404 like an unknown invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CompleteGatewayPayment_AnOwnDraft_IsNotFoundLikeAnAbsentInvoice()
    {
        var (owner, draftId) = await SeedDraftAsync("draft-gw-complete");

        var response = await owner.PostAsync($"/api/me/invoices/{draftId}/gateway-payment/complete", content: null);
        var absent = await owner.PostAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}/gateway-payment/complete", content: null);

        await AssertIndistinguishableFromAbsentAsync(response, absent);
    }

    /// <summary>The caller's own draft is left out of the invoice list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_AnOwnDraft_IsLeftOut()
    {
        var (owner, draftId) = await SeedDraftAsync("draft-list");

        var response = await owner.GetAsync("/api/me/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Should().NotContain(draftId);
    }

    /// <summary>Once staff publish the draft, the client reads it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_AfterItIsPublished_IsReadable()
    {
        var (owner, draftId) = await SeedDraftAsync("draft-published");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/billing/{draftId}/publish", content: null)).IsSuccessStatusCode.Should().BeTrue();

        var response = await owner.GetAsync($"/api/me/invoices/{draftId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
