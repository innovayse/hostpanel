using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo payment plugin's in-memory stored credential capability: the test amounts pick the outcome, a repeated
/// idempotency key never charges twice, and the key lookup answers for every path including an unknown outcome.
/// </summary>
public sealed class DemoStoredCredentialPluginTests
{
    /// <summary>ISO 4217 numeric code of USD, used as the charge currency.</summary>
    private const string Usd = "840";

    /// <summary>Builds a plugin over its own private book of methods and charges.</summary>
    /// <param name="state">The book to share between plugins, or null for a fresh one.</param>
    /// <returns>The plugin.</returns>
    private static DemoPaymentPlugin NewPlugin(DemoStoredCredentialState? state = null)
    {
        return new DemoPaymentPlugin(Usd, [Usd], state ?? new DemoStoredCredentialState());
    }

    /// <summary>Builds a charge for the given amount and idempotency key.</summary>
    /// <param name="amountMinor">The amount in minor units; its last two digits pick the demo outcome.</param>
    /// <param name="key">The idempotency key.</param>
    /// <returns>The charge request.</returns>
    private static StoredChargeInstruction Charge(long amountMinor, string key)
    {
        return new StoredChargeInstruction("demo-cus-1", "demo-pm-1", amountMinor, Usd, "INV-1", key, "https://shop.example/return");
    }

    /// <summary>An ordinary amount succeeds and reports the transaction, the amount and the currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_OrdinaryAmount_SucceedsAndReportsWhatWasTaken()
    {
        var result = await NewPlugin().ChargeAsync(Charge(1000, "key-ok"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Succeeded);
        result.TransactionId.Should().Be("demo-charge-key-ok");
        result.AmountMinor.Should().Be(1000);
        result.Currency.Should().Be(Usd);
        result.ActionUrl.Should().BeNull();
    }

    /// <summary>An amount ending in 02 is declined, which is a result and not an exception.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AmountEndingInTwo_IsDeclined()
    {
        var result = await NewPlugin().ChargeAsync(Charge(1002, "key-decline"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Declined);
        result.TransactionId.Should().BeNull();
    }

    /// <summary>An amount ending in 03 asks the payer to authenticate and carries the action URL.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AmountEndingInThree_RequiresAction()
    {
        var result = await NewPlugin().ChargeAsync(Charge(1003, "key-action"), CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.ActionRequired);
        result.ActionUrl.Should().Be("https://shop.example/return?demo=action");
        result.TransactionId.Should().BeNull();
    }

    /// <summary>The lookup of an action-required charge still carries the URL to send the payer to.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Find_ActionRequiredCharge_CarriesTheActionUrl()
    {
        var plugin = NewPlugin();
        await plugin.ChargeAsync(Charge(1003, "key-action-find"), CancellationToken.None);

        var lookup = await plugin.FindChargeAsync("demo-cus-1", "key-action-find", CancellationToken.None);

        lookup.Should().Be(new StoredChargeLookup(
            true, StoredChargeOutcome.ActionRequired, null, null, null, "https://shop.example/return?demo=action"));
    }

    /// <summary>An amount ending in 04 stays pending, and the lookup keeps reporting it pending.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AmountEndingInFour_StaysPendingAndTheLookupAgrees()
    {
        var plugin = NewPlugin();

        var result = await plugin.ChargeAsync(Charge(1004, "key-pending"), CancellationToken.None);
        var lookup = await plugin.FindChargeAsync("demo-cus-1", "key-pending", CancellationToken.None);

        result.Outcome.Should().Be(StoredChargeOutcome.Pending);
        result.TransactionId.Should().BeNull();
        lookup.Should().Be(new StoredChargeLookup(true, StoredChargeOutcome.Pending, null, null, null, null));
    }

    /// <summary>An amount ending in 05 takes the money and then fails to answer; the key lookup finds the charge.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AmountEndingInFive_ThrowsAfterChargingAndTheLookupFindsIt()
    {
        var plugin = NewPlugin();

        var act = () => plugin.ChargeAsync(Charge(1005, "key-lost-answer"), CancellationToken.None);
        await act.Should().ThrowAsync<TimeoutException>();
        var lookup = await plugin.FindChargeAsync("demo-cus-1", "key-lost-answer", CancellationToken.None);

        lookup.Found.Should().BeTrue();
        lookup.Outcome.Should().Be(StoredChargeOutcome.Succeeded);
        lookup.TransactionId.Should().Be("demo-charge-key-lost-answer");
        lookup.AmountMinor.Should().Be(1005);
        lookup.Currency.Should().Be(Usd);
    }

    /// <summary>An amount ending in 06 fails to answer before charging; the lookup finds nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AmountEndingInSix_ThrowsWithoutChargingAndTheLookupFindsNothing()
    {
        var state = new DemoStoredCredentialState();
        var plugin = NewPlugin(state);

        var act = () => plugin.ChargeAsync(Charge(1006, "key-never-charged"), CancellationToken.None);
        await act.Should().ThrowAsync<TimeoutException>();
        var lookup = await plugin.FindChargeAsync("demo-cus-1", "key-never-charged", CancellationToken.None);

        lookup.Should().Be(new StoredChargeLookup(false, null, null, null, null, null));
        state.ChargeCount.Should().Be(0);
    }

    /// <summary>A repeated charge with the same key answers the first result and charges only once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_RepeatedWithTheSameKey_DoesNotChargeTwice()
    {
        var state = new DemoStoredCredentialState();
        var plugin = NewPlugin(state);

        var first = await plugin.ChargeAsync(Charge(1000, "key-once"), CancellationToken.None);
        var second = await plugin.ChargeAsync(Charge(1000, "key-once"), CancellationToken.None);

        second.Should().Be(first);
        state.ChargeCount.Should().Be(1);
    }

    /// <summary>The first result for a key is final: a repeat with a different amount still answers the first result.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_RepeatedWithTheSameKeyButAnotherAmount_AnswersTheFirstResult()
    {
        var state = new DemoStoredCredentialState();
        var plugin = NewPlugin(state);

        var first = await plugin.ChargeAsync(Charge(1002, "key-final"), CancellationToken.None);
        var second = await plugin.ChargeAsync(Charge(1000, "key-final"), CancellationToken.None);

        first.Outcome.Should().Be(StoredChargeOutcome.Declined);
        second.Should().Be(first);
        state.ChargeCount.Should().Be(1);
    }

    /// <summary>A new key is a new attempt and charges again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_WithANewKey_ChargesAgain()
    {
        var state = new DemoStoredCredentialState();
        var plugin = NewPlugin(state);

        await plugin.ChargeAsync(Charge(1000, "key-a"), CancellationToken.None);
        await plugin.ChargeAsync(Charge(1000, "key-b"), CancellationToken.None);

        state.ChargeCount.Should().Be(2);
    }

    /// <summary>A key is remembered across plugin instances that share the book, as the resolver builds a new plugin per call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_RepeatedThroughAnotherInstanceOverTheSameBook_DoesNotChargeTwice()
    {
        var state = new DemoStoredCredentialState();

        await NewPlugin(state).ChargeAsync(Charge(1000, "key-shared"), CancellationToken.None);
        var lookup = await NewPlugin(state).FindChargeAsync("demo-cus-1", "key-shared", CancellationToken.None);
        await NewPlugin(state).ChargeAsync(Charge(1000, "key-shared"), CancellationToken.None);

        lookup.Found.Should().BeTrue();
        state.ChargeCount.Should().Be(1);
    }

    /// <summary>A key that never charged is reported as not found.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FindCharge_UnknownKey_IsNotFound()
    {
        var lookup = await NewPlugin().FindChargeAsync("demo-cus-1", "key-unknown", CancellationToken.None);

        lookup.Found.Should().BeFalse();
        lookup.Outcome.Should().BeNull();
    }

    /// <summary>A key longer than 64 characters or outside ASCII is refused, as the capability's rule says.</summary>
    /// <param name="key">The invalid key.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("ключ")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task Charge_InvalidKey_IsRefused(string key)
    {
        var plugin = NewPlugin();

        var act = () => plugin.ChargeAsync(Charge(1000, key), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Saving a method walks the embedded flow: customer, session, completion, list, default, remove.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SaveMethod_WalksTheWholeLifecycle()
    {
        var plugin = NewPlugin();

        var customer = await plugin.EnsureCustomerAsync(new CredentialCustomerDetails("client-7", null, "https://shop.test/return"), CancellationToken.None);
        var again = await plugin.EnsureCustomerAsync(new CredentialCustomerDetails("client-7", null, "https://shop.test/return"), CancellationToken.None);
        var first = await plugin.BeginSaveMethodAsync(new SaveMethodContext(customer.CustomerRef, "https://shop.test/return"), CancellationToken.None);
        var saved = await plugin.CompleteSaveMethodAsync(first.SessionId, CancellationToken.None);
        var second = await plugin.BeginSaveMethodAsync(new SaveMethodContext(customer.CustomerRef, "https://shop.test/return"), CancellationToken.None);
        var savedSecond = await plugin.CompleteSaveMethodAsync(second.SessionId, CancellationToken.None);

        again.Should().Be(customer);
        first.Mode.Should().Be(SaveMethodMode.EmbeddedToken);
        first.ClientToken.Should().NotBeNullOrEmpty();
        saved.Saved.Should().BeTrue();
        saved.Method!.IsDefault.Should().BeTrue();
        savedSecond.Method!.IsDefault.Should().BeFalse();

        await plugin.SetDefaultMethodAsync(customer.CustomerRef, savedSecond.Method.MethodRef, CancellationToken.None);
        (await plugin.ListMethodsAsync(customer.CustomerRef, CancellationToken.None))
            .Should().ContainSingle(m => m.IsDefault).Which.MethodRef.Should().Be(savedSecond.Method.MethodRef);

        await plugin.RemoveMethodAsync(customer.CustomerRef, savedSecond.Method.MethodRef, CancellationToken.None);
        (await plugin.ListMethodsAsync(customer.CustomerRef, CancellationToken.None))
            .Should().ContainSingle().Which.Should().Match<SavedMethod>(m => m.MethodRef == saved.Method.MethodRef && m.IsDefault);
    }

    /// <summary>A session that is unknown, or already completed, saves nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CompleteSaveMethod_UnknownOrRepeatedSession_SavesNothing()
    {
        var plugin = NewPlugin();
        var session = await plugin.BeginSaveMethodAsync(new SaveMethodContext("demo-cus-9", "https://shop.test/return"), CancellationToken.None);
        await plugin.CompleteSaveMethodAsync(session.SessionId, CancellationToken.None);

        var repeated = await plugin.CompleteSaveMethodAsync(session.SessionId, CancellationToken.None);
        var unknown = await plugin.CompleteSaveMethodAsync("no-such-session", CancellationToken.None);

        repeated.Should().Be(new SavedMethodResult(false, null));
        unknown.Should().Be(new SavedMethodResult(false, null));
        (await plugin.ListMethodsAsync("demo-cus-9", CancellationToken.None)).Should().ContainSingle();
    }
}
