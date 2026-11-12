using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// A hosted-gateway payment finished by the request the payer comes back with (a browser step): the host hands the return
/// parameters to the plugin that completes a payment from them, within the limits of the plugin contract and before it
/// reads the status, and reads the status alone for a plugin without that capability and for a check that is not a return,
/// such as the reconciliation job's.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteGatewayPaymentReturnTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The module every test registers its fake under.</summary>
    private const string Module = "bank-gateway";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver)
    {
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
    }

    /// <summary>The status a plugin reports once the invoice of these tests is paid; each call names its own reference.</summary>
    /// <returns>A paid status for the whole invoice.</returns>
    private static GatewayPaymentStatus Paid()
    {
        return new GatewayPaymentStatus(GatewayPaymentState.Paid, $"ref-{Guid.NewGuid():N}", "paid", AmountMinor: 1000);
    }

    /// <summary>Opens a browser step session for a fresh invoice of a fresh client.</summary>
    /// <param name="plugin">The plugin the module resolves to; it answers a browser step.</param>
    /// <param name="label">Distinguishes the client.</param>
    /// <returns>The host, the payer's client and the invoice with its open session.</returns>
    private async Task<(WebApplicationFactory<Program> Host, HttpClient Client, int InvoiceId)> OpenSessionAsync(IPaymentPlugin plugin, string label)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, label);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var started = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });
        started.StatusCode.Should().Be(HttpStatusCode.OK);
        return (host, client, invoiceId);
    }

    /// <summary>The payer comes back to the portal, which posts the return request's parameters.</summary>
    /// <param name="client">The payer's client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="returned">The names and values of the return request, or <see langword="null"/> for a check that is not a return.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, int invoiceId, params (string Name, string Value)[]? returned)
    {
        var body = returned is null
            ? (object)new { }
            : new { returnParameters = returned.Select(p => new { name = p.Name, value = p.Value }).ToArray() };
        return client.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", body);
    }

    /// <summary>The plugin is asked to complete the payment with the return parameters, then the status is read, and that status decides.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheReturnParameters_ReachThePlugin_BeforeTheStatusIsRead()
    {
        var plugin = new FakeReturnPaymentPlugin { StatusResult = Paid() };
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-order");

        var response = await CompleteAsync(client, invoiceId, ("nonce", "n1"), ("payer", "p1"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
        plugin.Calls.Should().Equal("complete", "status");
        plugin.Completions.Should().ContainSingle();
        plugin.Completions[0].GatewayOrderId.Should().Be("gw-step");
        plugin.Completions[0].Parameters.Should().BeEquivalentTo(new Dictionary<string, string> { ["nonce"] = "n1", ["payer"] = "p1" });
    }

    /// <summary>The first value of a repeated name wins and names are compared ordinally.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AReturn_WithARepeatedName_PassesTheFirstValue_AndNamesAreOrdinal()
    {
        var plugin = new FakeReturnPaymentPlugin();
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-first");

        (await CompleteAsync(client, invoiceId, ("token", "first"), ("Token", "upper"), ("token", "second"))).StatusCode.Should().Be(HttpStatusCode.OK);

        plugin.Completions.Should().ContainSingle();
        plugin.Completions[0].Parameters.Should().BeEquivalentTo(new Dictionary<string, string> { ["token"] = "first", ["Token"] = "upper" });
    }

    /// <summary>A return over a limit of the plugin contract reaches the plugin as an empty set, and the status is still read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AReturn_OverALimit_ReachesThePluginAsAnEmptySet()
    {
        var plugin = new FakeReturnPaymentPlugin();
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-limit");
        var tooMany = Enumerable.Range(0, PaymentReturnLimits.MaxParameters + 1).Select(i => ($"p{i}", "v")).ToArray();

        (await CompleteAsync(client, invoiceId, tooMany)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CompleteAsync(client, invoiceId, ("v", new string('x', PaymentReturnLimits.MaxValueLength + 1)))).StatusCode.Should().Be(HttpStatusCode.OK);

        plugin.Completions.Should().HaveCount(2);
        plugin.Completions.Should().OnlyContain(c => c.Parameters.Count == 0);
        plugin.Calls.Should().Equal("complete", "status", "complete", "status");
    }

    /// <summary>A return that carried nothing is still a return: the plugin is asked, with an empty set.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AReturn_ThatCarriedNothing_IsStillHandedToThePlugin()
    {
        var plugin = new FakeReturnPaymentPlugin();
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-empty");

        (await CompleteAsync(client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);

        plugin.Completions.Should().ContainSingle();
        plugin.Completions[0].Parameters.Should().BeEmpty();
    }

    /// <summary>A check that is not a return (no parameters were sent) only reads the status, as the reconciliation job does.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACheckThatIsNotAReturn_OnlyReadsTheStatus()
    {
        var plugin = new FakeReturnPaymentPlugin();
        var (host, client, invoiceId) = await OpenSessionAsync(plugin, "ret-none");

        (await CompleteAsync(client, invoiceId, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = host.Services.CreateScope();
        var viaBus = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result<GatewayCompletionDto>>(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        viaBus.IsSuccess.Should().BeTrue();
        plugin.Calls.Should().Equal("status", "status");
        plugin.Completions.Should().BeEmpty();
    }

    /// <summary>A plugin without the capability to complete a return is asked for the status only, parameters or not.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginWithoutTheCapability_IsOnlyAskedForTheStatus()
    {
        var plugin = new FakePaymentPlugin { StatusResult = Paid() };
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-plain");

        var response = await CompleteAsync(client, invoiceId, ("nonce", "n1"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>A plugin that throws while completing leaves the status unread and answers 503, so the payer can check again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatThrowsWhileCompleting_AnswersServiceUnavailable_WithoutReadingTheStatus()
    {
        const string gatewayText = "gateway fault 0xBEEF";
        var plugin = new FakeReturnPaymentPlugin { CompleteThrows = new HttpRequestException(gatewayText) };
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-throws");

        var response = await CompleteAsync(client, invoiceId, ("nonce", "n1"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(gatewayText);
        plugin.Calls.Should().Equal("complete");
    }

    /// <summary>A payer who returns twice does not complete a settled payment again: the plugin is not asked for an invoice that is paid.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APayerWhoReturnsTwice_DoesNotCompleteAPaidInvoiceAgain()
    {
        var plugin = new FakeReturnPaymentPlugin { StatusResult = Paid() };
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, "ret-twice");

        (await CompleteAsync(client, invoiceId, ("nonce", "n1"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await CompleteAsync(client, invoiceId, ("nonce", "n1"));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
        plugin.Completions.Should().ContainSingle();
    }

    /// <summary>A declined or pending outcome of the completion is reported as the status the plugin then shows.</summary>
    /// <param name="state">The state the plugin reports after the completion.</param>
    /// <param name="expected">The completion the payer is told.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(GatewayPaymentState.Pending, GatewayCompletionState.Pending)]
    [InlineData(GatewayPaymentState.Declined, GatewayCompletionState.Declined)]
    public async Task TheStatusAfterTheCompletion_DecidesWhatThePayerIsTold(GatewayPaymentState state, GatewayCompletionState expected)
    {
        var plugin = new FakeReturnPaymentPlugin { StatusResult = new GatewayPaymentStatus(state, null, "raw") };
        var (_, client, invoiceId) = await OpenSessionAsync(plugin, $"ret-{state}");

        var response = await CompleteAsync(client, invoiceId, ("nonce", "n1"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString().Should().Be(expected.ToString());
    }
}
