using System.Net;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Stripe;

/// <summary>
/// <see cref="StripeStoredCredentialPlugin"/>, the built-in saved-method capability, against a real loopback server
/// standing in for the Stripe API: how a customer is found or created, how a card is saved, how a charge answer is
/// told apart (paid, needs the cardholder, declined, still processing, unknown) and how a charge is found again by its key.
/// </summary>
public sealed class StripeStoredCredentialPluginTests : IAsyncLifetime
{
    /// <summary>The moment the plugin's clock reads, so the lookup window is exact.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The loopback server standing in for the Stripe API; started per test class instance.</summary>
    private StubHttpServer stripe = null!;

    /// <summary>Starts the stub Stripe server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        stripe = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub Stripe server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await stripe.DisposeAsync();
    }

    /// <summary>A customer found by the client reference is reused and nothing is created.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EnsureCustomerAsync_FindsTheCustomerByTheClientReferenceFirstAsync()
    {
        stripe.Enqueue("""{"object":"search_result","data":[{"id":"cus_found","object":"customer"}],"has_more":false}""");

        var result = await CreatePlugin().EnsureCustomerAsync(new CredentialCustomerContext("42", "ada@example.test", "Ada"), CancellationToken.None);

        result.CustomerRef.Should().Be("cus_found");
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().StartWith("/v1/customers/search").And.Contain("hostpanelClientId").And.Contain("42");
    }

    /// <summary>With no customer found one is created with the client reference on it and a stable idempotency key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EnsureCustomerAsync_NoneFound_CreatesOneUnderAnIdempotencyKeyAsync()
    {
        stripe.Enqueue("""{"object":"search_result","data":[],"has_more":false}""");
        stripe.Enqueue("""{"id":"cus_77","object":"customer"}""");

        var result = await CreatePlugin().EnsureCustomerAsync(new CredentialCustomerContext("42", "ada@example.test", "Ada Lovelace"), CancellationToken.None);

        result.CustomerRef.Should().Be("cus_77");
        stripe.Requests.Should().HaveCount(2);
        var create = stripe.Requests[1];
        create.PathAndQuery.Should().Be("/v1/customers");
        create.Body.Should().Contain("metadata[hostpanelClientId]=42").And.Contain("email=ada%40example.test");
        create.IdempotencyKey.Should().Be("customer-create-client-42");
    }

    /// <summary>A save session is a setup intent for the customer, kept for off-session use, handed to the browser as a token.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BeginSaveMethodAsync_OpensAnOffSessionSetupIntentAsAnEmbeddedTokenAsync()
    {
        stripe.Enqueue("""{"id":"seti_1","object":"setup_intent","client_secret":"seti_1_secret","status":"requires_payment_method"}""");

        var session = await CreatePlugin().BeginSaveMethodAsync(new SaveMethodContext("cus_77", "https://panel.example.test/back"), CancellationToken.None);

        session.SessionId.Should().Be("seti_1");
        session.Mode.Should().Be(SaveMethodMode.EmbeddedToken);
        session.ClientToken.Should().Be("seti_1_secret");
        session.RedirectUrl.Should().BeNull();
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/v1/setup_intents");
        call.Body.Should().Contain("customer=cus_77").And.Contain("usage=off_session");
        call.IdempotencyKey.Should().StartWith("setup-cus_77-");
    }

    /// <summary>A succeeded setup intent gives the saved card, flagged as the default when it is the customer's default.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CompleteSaveMethodAsync_Succeeded_ReturnsTheSavedCardAsync()
    {
        stripe.Enqueue("""{"id":"seti_1","object":"setup_intent","status":"succeeded","customer":"cus_77","payment_method":"pm_9"}""");
        stripe.Enqueue("""{"id":"cus_77","object":"customer","invoice_settings":{"default_payment_method":"pm_9"}}""");
        stripe.Enqueue("""{"id":"pm_9","object":"payment_method","type":"card","card":{"brand":"visa","last4":"4242","exp_month":12,"exp_year":2035}}""");

        var result = await CreatePlugin().CompleteSaveMethodAsync("seti_1", CancellationToken.None);

        result.Saved.Should().BeTrue();
        result.Method.Should().Be(new SavedMethod("pm_9", "visa", "4242", 12, 2035, true));
    }

    /// <summary>A setup intent that has not succeeded saves nothing and is a normal answer, not an error.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CompleteSaveMethodAsync_NotSucceeded_SavesNothingAsync()
    {
        stripe.Enqueue("""{"id":"seti_2","object":"setup_intent","status":"requires_action","customer":"cus_77","payment_method":"pm_10"}""");

        var result = await CreatePlugin().CompleteSaveMethodAsync("seti_2", CancellationToken.None);

        result.Saved.Should().BeFalse();
        result.Method.Should().BeNull();
        stripe.Requests.Should().ContainSingle();
    }

    /// <summary>A charge that went through reports the charge id, the amount in ISO minor units and the upper-case currency.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeAsync_Succeeded_ReportsTheChargeAndWhatWasTakenAsync()
    {
        stripe.Enqueue(IntentBody(200000, "ugx", "succeeded"));

        var result = await CreatePlugin().ChargeAsync(Charge(2000, "UGX", "invoice-9-auto-a"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Succeeded);
        result.TransactionId.Should().Be("ch_1");
        result.AmountMinor.Should().Be(2000);
        result.Currency.Should().Be("UGX");
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/v1/payment_intents");
        call.Body.Should().Contain("amount=200000").And.Contain("customer=cus_1").And.Contain("payment_method=pm_1")
            .And.Contain("off_session=true").And.Contain("confirm=true")
            .And.Contain("metadata[attemptKey]=invoice-9-auto-a").And.Contain("metadata[invoiceId]=9");
        call.IdempotencyKey.Should().Be("invoice-9-auto-a");
    }

    /// <summary>A card Stripe says needs the cardholder (error code, or a requires-action intent) is told apart from a decline.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeAsync_AuthenticationRequired_IsActionRequiredAsync()
    {
        stripe.Enqueue(HttpStatusCode.PaymentRequired, ErrorBody("card_error", "authentication_required"));
        stripe.Enqueue(IntentBody(1000, "usd", "requires_action"));
        var plugin = CreatePlugin();

        var byCode = await plugin.ChargeAsync(Charge(1000, "USD", "key-a"), CancellationToken.None);
        var byStatus = await plugin.ChargeAsync(Charge(1000, "USD", "key-b"), CancellationToken.None);

        byCode.Outcome.Should().Be(StoredChargeOutcome.ActionRequired);
        byStatus.Outcome.Should().Be(StoredChargeOutcome.ActionRequired);
        byCode.TransactionId.Should().BeNull();
    }

    /// <summary>Any other card error is a decline, a result and not an exception.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeAsync_OtherCardError_IsDeclinedAsync()
    {
        stripe.Enqueue(HttpStatusCode.PaymentRequired, ErrorBody("card_error", "card_declined"));

        var result = await CreatePlugin().ChargeAsync(Charge(1000, "USD", "key-a"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Declined);
        result.TransactionId.Should().BeNull();
    }

    /// <summary>A payment Stripe is still processing is Pending, never read as paid or as refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeAsync_Processing_IsPendingAsync()
    {
        stripe.Enqueue(IntentBody(1000, "usd", "processing"));

        var result = await CreatePlugin().ChargeAsync(Charge(1000, "USD", "key-a"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Pending);
        result.TransactionId.Should().BeNull();
    }

    /// <summary>An outage (5xx), a rate limit and a lost connection are exceptions: the outcome is unknown and the host must look the charge up.</summary>
    /// <param name="status">The status Stripe answers with.</param>
    /// <param name="type">The error type in the body.</param>
    /// <param name="code">The error code in the body.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "api_error", "unexpected")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", "rate_limit")]
    [InlineData(HttpStatusCode.Conflict, "idempotency_error", "idempotency_key_in_use")]
    public async Task ChargeAsync_UnknownOutcome_Throws(HttpStatusCode status, string type, string code)
    {
        stripe.Enqueue(status, ErrorBody(type, code));

        var charge = () => CreatePlugin().ChargeAsync(Charge(1000, "USD", "key-a"), CancellationToken.None);

        await charge.Should().ThrowAsync<global::Stripe.StripeException>();
    }

    /// <summary>A lost connection is an exception too.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeAsync_ConnectionRefused_ThrowsAsync()
    {
        var plugin = CreatePlugin(apiBase: "http://127.0.0.1:1");

        var charge = () => plugin.ChargeAsync(Charge(1000, "USD", "key-a"), CancellationToken.None);

        await charge.Should().ThrowAsync<Exception>();
    }

    /// <summary>A lookup lists the customer's intents created within the window and finds the one carrying the key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FindChargeAsync_FindsTheIntentByItsKeyWithinTheLookupWindowAsync()
    {
        stripe.Enqueue(
            $$$"""{"object":"list","has_more":false,"data":[{"id":"pi_other","object":"payment_intent","amount":500,"currency":"usd","status":"succeeded","metadata":{"attemptKey":"other"}},{{{IntentBody(1000, "usd", "succeeded", "key-a")}}}]}""");

        var lookup = await CreatePlugin().FindChargeAsync("cus_1", "key-a", CancellationToken.None);

        lookup.Should().Be(new StoredChargeLookup(true, StoredChargeOutcome.Succeeded, "ch_1", 1000, "USD", null));
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().StartWith("/v1/payment_intents").And.Contain("customer=cus_1");
        var expectedFloor = Now.AddDays(-14).ToUnixTimeSeconds();
        call.PathAndQuery.Should().Contain($"created[gte]={expectedFloor}");
    }

    /// <summary>A lookup that finds no intent with the key says so, which the host trusts only because the call itself succeeded.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FindChargeAsync_NoIntentWithTheKey_IsNotFoundAsync()
    {
        stripe.Enqueue("""{"object":"list","has_more":false,"data":[{"id":"pi_other","object":"payment_intent","amount":500,"currency":"usd","status":"succeeded","metadata":{"attemptKey":"other"}}]}""");

        var lookup = await CreatePlugin().FindChargeAsync("cus_1", "key-a", CancellationToken.None);

        lookup.Found.Should().BeFalse();
        lookup.Outcome.Should().BeNull();
    }

    /// <summary>A lookup the gateway cannot answer throws rather than saying nothing was found.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FindChargeAsync_Outage_ThrowsAsync()
    {
        stripe.Enqueue(HttpStatusCode.InternalServerError, ErrorBody("api_error", "unexpected"));

        var lookup = () => CreatePlugin().FindChargeAsync("cus_1", "key-a", CancellationToken.None);

        await lookup.Should().ThrowAsync<global::Stripe.StripeException>();
    }

    /// <summary>The customer's cards are listed with the customer's default flagged.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListMethodsAsync_ListsTheCardsAndFlagsTheDefaultAsync()
    {
        stripe.Enqueue("""{"id":"cus_1","object":"customer","invoice_settings":{"default_payment_method":"pm_b"}}""");
        stripe.Enqueue("""{"object":"list","has_more":false,"data":[{"id":"pm_a","object":"payment_method","type":"card","card":{"brand":"visa","last4":"4242","exp_month":1,"exp_year":2030}},{"id":"pm_b","object":"payment_method","type":"card","card":{"brand":"mastercard","last4":"4444","exp_month":2,"exp_year":2031}}]}""");

        var methods = await CreatePlugin().ListMethodsAsync("cus_1", CancellationToken.None);

        methods.Should().Equal(
            new SavedMethod("pm_a", "visa", "4242", 1, 2030, false),
            new SavedMethod("pm_b", "mastercard", "4444", 2, 2031, true));
        stripe.Requests[1].PathAndQuery.Should().StartWith("/v1/payment_methods").And.Contain("customer=cus_1").And.Contain("type=card");
    }

    /// <summary>Making a card the default sets it as the customer's invoice default.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SetDefaultMethodAsync_SetsTheCustomersInvoiceDefaultAsync()
    {
        stripe.Enqueue("""{"id":"cus_1","object":"customer"}""");

        await CreatePlugin().SetDefaultMethodAsync("cus_1", "pm_b", CancellationToken.None);

        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/v1/customers/cus_1");
        call.Body.Should().Contain("invoice_settings[default_payment_method]=pm_b");
    }

    /// <summary>Removing a card detaches it from the customer.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RemoveMethodAsync_DetachesTheCardAsync()
    {
        stripe.Enqueue("""{"id":"pm_b","object":"payment_method","type":"card"}""");

        await CreatePlugin().RemoveMethodAsync("cus_1", "pm_b", CancellationToken.None);

        stripe.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/v1/payment_methods/pm_b/detach");
    }

    /// <summary>Without a secret key the plugin reports itself as not configured, so the resolver never offers it.</summary>
    [Fact]
    public void IsConfigured_WithoutASecretKey_IsFalse()
    {
        CreatePlugin(secretKey: "").IsConfigured.Should().BeFalse();
        CreatePlugin().IsConfigured.Should().BeTrue();
    }

    /// <summary>Builds a charge instruction.</summary>
    /// <param name="amountMinor">The amount in ISO minor units.</param>
    /// <param name="currency">The ISO currency code.</param>
    /// <param name="key">The idempotency key.</param>
    /// <returns>The instruction.</returns>
    private static StoredChargeInstruction Charge(long amountMinor, string currency, string key) =>
        new("cus_1", "pm_1", amountMinor, currency, "9", key, "https://panel.example.test/pay");

    /// <summary>Builds a payment intent body in the shape Stripe answers with.</summary>
    /// <param name="amount">The amount in Stripe's units.</param>
    /// <param name="currency">The lowercase currency code.</param>
    /// <param name="status">The intent status.</param>
    /// <param name="attemptKey">The attempt key stored on the intent.</param>
    /// <returns>The JSON body.</returns>
    private static string IntentBody(long amount, string currency, string status, string attemptKey = "key") =>
        $$$"""{"id":"pi_1","object":"payment_intent","amount":{{{amount}}},"currency":"{{{currency}}}","status":"{{{status}}}","latest_charge":"ch_1","metadata":{"attemptKey":"{{{attemptKey}}}","invoiceId":"9"}}""";

    /// <summary>Builds a Stripe error body.</summary>
    /// <param name="type">The error type.</param>
    /// <param name="code">The error code.</param>
    /// <returns>The JSON body.</returns>
    private static string ErrorBody(string type, string code) =>
        $$$"""{"error":{"type":"{{{type}}}","code":"{{{code}}}","message":"stub"}}""";

    /// <summary>Builds a plugin aimed at the stub server.</summary>
    /// <param name="secretKey">The secret key, empty for an unconfigured gateway.</param>
    /// <param name="apiBase">The API address, the stub server's by default.</param>
    /// <returns>The plugin under test.</returns>
    private StripeStoredCredentialPlugin CreatePlugin(string secretKey = "sk_test_stub", string? apiBase = null)
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = secretKey,
            ApiBaseUrl = apiBase ?? stripe.BaseAddress.ToString().TrimEnd('/'),
        });
        return new StripeStoredCredentialPlugin(options, new StripeClientProvider(options), new FixedClock(Now), NullLogger<StripeStoredCredentialPlugin>.Instance);
    }

    /// <summary>A clock that always reads the same moment.</summary>
    /// <param name="now">The moment it reads.</param>
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => now;
    }
}
