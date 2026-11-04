using System.Globalization;
using global::Stripe;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Stripe;

/// <summary>
/// <see cref="StripeStoredCredentialPlugin"/> against the real Stripe API in Test Mode, with Stripe's test payment methods:
/// what a stub cannot prove, namely how Stripe itself answers a saved card, an off-session charge, an authentication
/// request, a decline, a repeated idempotency key and a look-up by key. Skipped unless <c>STRIPE_TEST_SECRET_KEY</c> holds a
/// <c>sk_test_</c> key. Every object it makes is a test object on the account that key belongs to.
/// </summary>
/// <param name="output">Receives the Stripe ids each scenario produced, as evidence for the pull request.</param>
public sealed class StripeTestModeTests(ITestOutputHelper output)
{
    /// <summary>The Stripe API client of the test key, used to attach Stripe's test payment methods.</summary>
    private readonly StripeClient _api = new(Environment.GetEnvironmentVariable(LiveStripeFactAttribute.KeyVariable));

    /// <summary>Saving a card, charging it off-session, repeating the key, and finding the charge again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [LiveStripeFact]
    public async Task SavedCard_IsChargedOnceAndFoundByItsKeyAsync()
    {
        var plugin = CreatePlugin();
        var customer = await plugin.EnsureCustomerAsync(new CredentialCustomerContext(NewReference(), "test@example.test", "Test Mode"), CancellationToken.None);

        var session = await plugin.BeginSaveMethodAsync(new SaveMethodContext(customer.CustomerRef, "https://portal.example.test/back"), CancellationToken.None);
        session.Mode.Should().Be(SaveMethodMode.EmbeddedToken);
        session.ClientToken.Should().NotBeNullOrEmpty();

        // The browser would confirm the SetupIntent with Stripe.js; the test confirms it with Stripe's test Visa.
        await new SetupIntentService(_api).ConfirmAsync(session.SessionId, new SetupIntentConfirmOptions { PaymentMethod = "pm_card_visa" });
        var saved = await plugin.CompleteSaveMethodAsync(session.SessionId, CancellationToken.None);
        saved.Saved.Should().BeTrue();
        saved.Method!.Last4.Should().Be("4242");

        var methods = await plugin.ListMethodsAsync(customer.CustomerRef, CancellationToken.None);
        methods.Should().ContainSingle(m => m.MethodRef == saved.Method.MethodRef);
        await plugin.SetDefaultMethodAsync(customer.CustomerRef, saved.Method.MethodRef, CancellationToken.None);
        (await plugin.ListMethodsAsync(customer.CustomerRef, CancellationToken.None)).Single().IsDefault.Should().BeTrue();

        var key = $"live-test-{Guid.NewGuid():N}";
        var instruction = new StoredChargeInstruction(customer.CustomerRef, saved.Method.MethodRef, 1000, "USD", "live-invoice-1", key, null);
        var charged = await plugin.ChargeAsync(instruction, CancellationToken.None);
        charged.Outcome.Should().Be(StoredChargeOutcome.Succeeded);
        charged.AmountMinor.Should().Be(1000);
        charged.Currency.Should().Be("USD");

        var repeated = await plugin.ChargeAsync(instruction, CancellationToken.None);
        repeated.TransactionId.Should().Be(charged.TransactionId, "the same key is one charge at Stripe");

        var found = await plugin.FindChargeAsync(customer.CustomerRef, key, CancellationToken.None);
        found.Found.Should().BeTrue();
        found.Outcome.Should().Be(StoredChargeOutcome.Succeeded);
        found.TransactionId.Should().Be(charged.TransactionId);
        found.AmountMinor.Should().Be(1000);

        (await plugin.FindChargeAsync(customer.CustomerRef, $"never-used-{Guid.NewGuid():N}", CancellationToken.None)).Found.Should().BeFalse();

        output.WriteLine($"customer={customer.CustomerRef} setupIntent={session.SessionId} method={saved.Method.MethodRef} charge={charged.TransactionId} key={key}");
        await plugin.RemoveMethodAsync(customer.CustomerRef, saved.Method.MethodRef, CancellationToken.None);
        (await plugin.ListMethodsAsync(customer.CustomerRef, CancellationToken.None)).Should().BeEmpty();
    }

    /// <summary>The cards that need the cardholder, or that Stripe declines, are told apart from a paid charge.</summary>
    /// <param name="testMethod">Stripe's test payment method.</param>
    /// <param name="expected">The outcome the host must read from Stripe's answer.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [LiveStripeTheory]
    [InlineData("pm_card_authenticationRequired", StoredChargeOutcome.ActionRequired)]
    [InlineData("pm_card_chargeCustomerFail", StoredChargeOutcome.Declined)]
    public async Task OffSessionCharge_OfAStripeTestCard_IsMappedToItsOutcomeAsync(string testMethod, StoredChargeOutcome expected)
    {
        var plugin = CreatePlugin();
        var customer = await plugin.EnsureCustomerAsync(new CredentialCustomerContext(NewReference(), "test@example.test", "Test Mode"), CancellationToken.None);
        var attached = await new PaymentMethodService(_api).AttachAsync(testMethod, new PaymentMethodAttachOptions { Customer = customer.CustomerRef });

        var key = $"live-test-{Guid.NewGuid():N}";
        var charged = await plugin.ChargeAsync(
            new StoredChargeInstruction(customer.CustomerRef, attached.Id, 2000, "USD", "live-invoice-2", key, null), CancellationToken.None);

        charged.Outcome.Should().Be(expected);
        charged.TransactionId.Should().BeNull("nothing was taken");
        var found = await plugin.FindChargeAsync(customer.CustomerRef, key, CancellationToken.None);
        found.Found.Should().BeTrue("Stripe keeps the intent of a refused charge, so the host can look it up");
        found.Outcome.Should().Be(expected);
        output.WriteLine($"{testMethod}: customer={customer.CustomerRef} method={attached.Id} outcome={charged.Outcome} key={key}");
    }

    /// <summary>Parallel requests for one client end with one Stripe customer.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [LiveStripeFact]
    public async Task EnsureCustomer_InParallel_ReturnsOneCustomerAsync()
    {
        var plugin = CreatePlugin();
        var reference = NewReference();

        var customers = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            plugin.EnsureCustomerAsync(new CredentialCustomerContext(reference, "test@example.test", "Test Mode"), CancellationToken.None)));

        customers.Select(c => c.CustomerRef).Distinct().Should().ContainSingle();
        output.WriteLine($"reference={reference} customer={customers[0].CustomerRef}");
    }

    /// <summary>Builds the capability on the test key and the real Stripe API.</summary>
    /// <returns>The capability.</returns>
    private static StripeStoredCredentialPlugin CreatePlugin()
    {
        var options = Options.Create(new StripeOptions { SecretKey = Environment.GetEnvironmentVariable(LiveStripeFactAttribute.KeyVariable)! });
        return new StripeStoredCredentialPlugin(options, new StripeClientProvider(options), new SystemClock(), NullLogger<StripeStoredCredentialPlugin>.Instance);
    }

    /// <summary>A client reference no earlier run used, so a customer is never found from an older run.</summary>
    /// <returns>The reference.</returns>
    private static string NewReference() => string.Create(CultureInfo.InvariantCulture, $"live-{Guid.NewGuid():N}");

    /// <summary>The wall clock.</summary>
    private sealed class SystemClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
