using System.Net;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Stripe;

/// <summary>
/// <see cref="StripeService"/> against a real loopback server standing in for the Stripe API: the
/// amounts it sends and reads, the idempotency keys it attaches, and how a refusal, a rate limit
/// and an outage are told apart.
/// </summary>
public sealed class StripeServiceTests : IAsyncLifetime
{
    /// <summary>A refund body in the shape Stripe answers with.</summary>
    private const string RefundBody = """{"id":"re_1","object":"refund","status":"succeeded"}""";

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

    /// <summary>A payment intent is opened with the amount in Stripe's units and the instruction's idempotency key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_SendsStripeUnitsAndTheIdempotencyKeyAsync()
    {
        stripe.Enqueue(IntentBody(150000, "isk"));

        var result = await CreateService().CreatePaymentIntentAsync(
            new CardPaymentIntentInstruction(1500m, "ISK", 4, 9, 2, "invoice-9-intent-1500-ISK"), CancellationToken.None);

        result.Failure.Should().BeNull();
        result.ClientSecret.Should().Be("pi_1_secret");
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/v1/payment_intents");
        call.Body.Should().Contain("amount=150000").And.Contain("currency=isk");
        call.IdempotencyKey.Should().Be("invoice-9-intent-1500-ISK");
    }

    /// <summary>A three-decimal currency is sent with its last digit zero.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_EndsAThreeDecimalAmountInZeroAsync()
    {
        stripe.Enqueue(IntentBody(1240, "kwd"));

        await CreateService().CreatePaymentIntentAsync(
            new CardPaymentIntentInstruction(1.235m, "KWD", 4, 9, 2, "key"), CancellationToken.None);

        stripe.Requests.Should().ContainSingle().Which.Body.Should().Contain("amount=1240");
    }

    /// <summary>A payment intent Stripe reports in its own units is read back as the ISO minor units the settlement compares.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task VerifyPaymentIntentAsync_ReadsTheAmountBackInIsoMinorUnitsAsync()
    {
        stripe.Enqueue(IntentBody(150000, "isk"));

        var status = await CreateService().VerifyPaymentIntentAsync(
            new CardPaymentVerifyInstruction("pi_1"), CancellationToken.None);

        status.Succeeded.Should().BeTrue();
        status.AmountMinor.Should().Be(1500);
        status.Currency.Should().Be("isk");
        status.OrderId.Should().Be(4);
        status.InvoiceId.Should().Be(9);
    }

    /// <summary>A saved-method charge sends Stripe's units and its idempotency key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_SendsStripeUnitsAndTheIdempotencyKeyAsync()
    {
        stripe.Enqueue(IntentBody(200000, "ugx"));

        var result = await CreateService().ChargeSavedPaymentMethodAsync(
            new CardChargeInstruction("cus_1", "pm_1", 2000m, "UGX", 9, "invoice-9-attempt-a"), CancellationToken.None);

        result.Success.Should().BeTrue();
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.Body.Should().Contain("amount=200000");
        call.IdempotencyKey.Should().Be("invoice-9-attempt-a");
    }

    /// <summary>A refund sends Stripe's units and its idempotency key, against the charge when a charge id is given.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefundAsync_SendsStripeUnitsAndTheIdempotencyKeyAsync()
    {
        stripe.Enqueue(RefundBody);

        var result = await CreateService().RefundAsync(
            new CardRefundInstruction("ch_1", 1500m, "ISK", "refund-key"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RefundId.Should().Be("re_1");
        var call = stripe.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/v1/refunds");
        call.Body.Should().Contain("amount=150000").And.Contain("charge=ch_1");
        call.IdempotencyKey.Should().Be("refund-key");
    }

    /// <summary>A 4xx that refuses the request is a refusal, so the caller is not told to retry.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_MapsAClientErrorToRefusedAsync()
    {
        stripe.Enqueue(HttpStatusCode.BadRequest, ErrorBody("invalid_request_error", "parameter_invalid_integer"));

        var result = await CreateService().CreatePaymentIntentAsync(Instruction(), CancellationToken.None);

        result.ClientSecret.Should().BeNull();
        result.Failure.Should().Be(CardGatewayFailure.Refused);
    }

    /// <summary>A 429 says nothing about whether the request was carried out, so it is an outage, not a refusal.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_MapsRateLimitingToUnavailableAsync()
    {
        stripe.Enqueue(HttpStatusCode.TooManyRequests, ErrorBody("rate_limit_error", "rate_limit"));

        var result = await CreateService().CreatePaymentIntentAsync(Instruction(), CancellationToken.None);

        result.Failure.Should().Be(CardGatewayFailure.Unavailable);
    }

    /// <summary>A 409 for an idempotency key still in use means the first request may still complete, so it is an outage.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_MapsAKeyInUseToUnavailableAsync()
    {
        stripe.Enqueue(HttpStatusCode.Conflict, ErrorBody("idempotency_error", "idempotency_key_in_use"));

        var result = await CreateService().CreatePaymentIntentAsync(Instruction(), CancellationToken.None);

        result.Failure.Should().Be(CardGatewayFailure.Unavailable);
    }

    /// <summary>A 5xx is an outage.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreatePaymentIntentAsync_MapsAServerErrorToUnavailableAsync()
    {
        stripe.Enqueue(HttpStatusCode.InternalServerError, ErrorBody("api_error", "unexpected"));

        var result = await CreateService().CreatePaymentIntentAsync(Instruction(), CancellationToken.None);

        result.Failure.Should().Be(CardGatewayFailure.Unavailable);
    }

    /// <summary>A refund Stripe refuses with a 4xx (already refunded) is an unsuccessful result.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefundAsync_AnswersAnUnsuccessfulResultForARefusalAsync()
    {
        stripe.Enqueue(HttpStatusCode.BadRequest, ErrorBody("invalid_request_error", "charge_already_refunded"));

        var result = await CreateService().RefundAsync(
            new CardRefundInstruction("pi_1", 10m, "USD", "refund-key"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RefundId.Should().BeNull();
    }

    /// <summary>A refund whose outcome is unknown (a 429, a 409 key in use or a 5xx) throws, so the money is never assumed unrefunded.</summary>
    /// <param name="status">The status Stripe answers with.</param>
    /// <param name="type">The error type in the body.</param>
    /// <param name="code">The error code in the body.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", "rate_limit")]
    [InlineData(HttpStatusCode.Conflict, "idempotency_error", "idempotency_key_in_use")]
    [InlineData(HttpStatusCode.BadGateway, "api_error", "unexpected")]
    public async Task RefundAsync_ThrowsWhenTheOutcomeIsUnknownAsync(HttpStatusCode status, string type, string code)
    {
        stripe.Enqueue(status, ErrorBody(type, code));

        var refund = () => CreateService().RefundAsync(
            new CardRefundInstruction("pi_1", 10m, "USD", "refund-key"), CancellationToken.None);

        await refund.Should().ThrowAsync<global::Stripe.StripeException>();
    }

    /// <summary>
    /// With no secret key every member refuses with <see cref="CardGatewayFailure.NotConfigured"/>
    /// instead of throwing on the empty key, and nothing reaches Stripe.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EveryMember_WithoutASecretKey_RefusesNotConfiguredAndCallsNothingAsync()
    {
        var service = new StripeService(
            Options.Create(new StripeOptions { ApiBaseUrl = stripe.BaseAddress.ToString().TrimEnd('/') }),
            NullLogger<StripeService>.Instance);
        var ct = CancellationToken.None;

        service.IsConfigured.Should().BeFalse();
        (await service.CreatePaymentIntentAsync(Instruction(), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        (await service.VerifyPaymentIntentAsync(new CardPaymentVerifyInstruction("pi_1"), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        (await service.ChargeSavedPaymentMethodAsync(new CardChargeInstruction("cus_1", "pm_1", 10m, "USD", 9, "key"), ct))
            .Should().Match<CardChargeResult>(r => !r.Success && r.Failure == CardGatewayFailure.NotConfigured);
        (await service.FindChargeAsync(new CardChargeLookupInstruction("cus_1", 9, "key"), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        (await service.RefundAsync(new CardRefundInstruction("pi_1", 10m, "USD", "key"), ct)).Success.Should().BeFalse();
        (await service.ListPaymentMethodsAsync(new CardCustomerInstruction("cus_1"), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        (await service.SetDefaultPaymentMethodAsync(new CardDefaultMethodInstruction("cus_1", "pm_1"), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        (await service.DetachPaymentMethodAsync(new CardDetachInstruction("pm_1"), ct)).Failure.Should().Be(CardGatewayFailure.NotConfigured);
        stripe.Requests.Should().BeEmpty();
    }

    /// <summary>Builds a payment intent body in the shape Stripe answers with.</summary>
    /// <param name="amount">The amount in Stripe's units.</param>
    /// <param name="currency">The lowercase currency code.</param>
    /// <returns>The JSON body.</returns>
    private static string IntentBody(long amount, string currency) =>
        $$$"""{"id":"pi_1","object":"payment_intent","client_secret":"pi_1_secret","amount":{{{amount}}},"currency":"{{{currency}}}","status":"succeeded","latest_charge":"ch_1","metadata":{"orderId":"4","invoiceId":"9"}}""";

    /// <summary>Builds a service aimed at the stub server.</summary>
    /// <returns>The service under test.</returns>
    private StripeService CreateService() =>
        new(
            Options.Create(new StripeOptions
            {
                SecretKey = "sk_test_stub",
                ApiBaseUrl = stripe.BaseAddress.ToString().TrimEnd('/'),
            }),
            NullLogger<StripeService>.Instance);

    /// <summary>Builds a payment intent instruction in an ordinary currency.</summary>
    /// <returns>The instruction.</returns>
    private static CardPaymentIntentInstruction Instruction() =>
        new(10m, "USD", 4, 9, 2, "invoice-9-intent-1000-USD");

    /// <summary>Builds a Stripe error body.</summary>
    /// <param name="type">The error type.</param>
    /// <param name="code">The error code.</param>
    /// <returns>The JSON body.</returns>
    private static string ErrorBody(string type, string code) =>
        $$$"""{"error":{"type":"{{{type}}}","code":"{{{code}}}","message":"stub"}}""";
}
