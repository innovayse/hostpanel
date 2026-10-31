using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="ICardPaymentGateway"/> whose behaviour a test controls through its properties and
/// which records what it was asked to do. A fake port standing in for the card payment gateway
/// itself, which a test must not call for real.
/// </summary>
public sealed class FakeCardPaymentGateway : ICardPaymentGateway
{
    /// <summary>Guards <see cref="RefundInstructions"/> and <see cref="RefundsByKey"/>, which concurrent requests write.</summary>
    private readonly object _refundLock = new();

    /// <inheritdoc/>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// The payment methods <see cref="ListPaymentMethodsAsync"/> answers with, keyed by nothing:
    /// every configured customer id sees the same list, since the tests that use this fake
    /// exercise exactly one seeded client's saved cards at a time.
    /// </summary>
    public IReadOnlyList<CardPaymentMethodDto> SavedPaymentMethods { get; set; } = [];

    /// <summary>
    /// The failure <see cref="ListPaymentMethodsAsync"/> answers with instead of the list, as a
    /// gateway that refuses or cannot be reached would; null to list.
    /// </summary>
    public CardGatewayFailure? ListFailure { get; set; }

    /// <summary>
    /// The failure <see cref="SetDefaultPaymentMethodAsync"/> and <see cref="DetachPaymentMethodAsync"/>
    /// answer with instead of applying the change; null to apply it.
    /// </summary>
    public CardGatewayFailure? MethodChangeFailure { get; set; }

    /// <summary>The failure <see cref="FindChargeAsync"/> answers with instead of looking; null to look.</summary>
    public CardGatewayFailure? LookupFailure { get; set; }

    /// <summary>The client secret <see cref="CreatePaymentIntentAsync"/> answers with.</summary>
    public string IntentClientSecret { get; set; } = "pi_test_secret_1";

    /// <summary>
    /// The failure <see cref="CreatePaymentIntentAsync"/> answers with instead of an intent, as a
    /// gateway that refuses or cannot be reached would; null to open the intent.
    /// </summary>
    public CardGatewayFailure? IntentFailure { get; set; }

    /// <summary>What <see cref="VerifyPaymentIntentAsync"/> answers with; a test sets the amount, currency and ids it expects.</summary>
    public CardPaymentIntentStatus Verification { get; set; } = new(true, "txn-confirmed-1", 0, "usd", null, null);

    /// <summary>
    /// The refund id the first issued refund answers with; each later one adds its issue number,
    /// since a real gateway never issues the same refund id twice.
    /// </summary>
    public string RefundId { get; set; } = "re_test_1";

    /// <summary>Whether <see cref="RefundAsync"/> refuses every refund, as a gateway that declines it would.</summary>
    public bool RefusesRefunds { get; set; }

    /// <summary>
    /// The 1-based <see cref="RefundAsync"/> call that throws instead of answering, as a gateway that
    /// times out or faults part way through a refund would; null when no call throws.
    /// </summary>
    public int? ThrowsOnRefundCall { get; set; }

    /// <summary>Every instruction <see cref="RefundAsync"/> was called with, in order.</summary>
    public List<CardRefundInstruction> RefundInstructions { get; } = [];

    /// <summary>
    /// Every refund the gateway issued, by idempotency key. A key it has seen answers with its first
    /// refund instead of issuing another, as a real gateway replays it.
    /// </summary>
    public Dictionary<string, CardRefundResult> RefundsByKey { get; } = [];

    /// <summary>
    /// Runs after a refund is issued and before the gateway answers; null for none. A test throws
    /// from it to lose the answer of a refund that did happen, or writes to the database meanwhile.
    /// </summary>
    public Func<CardRefundInstruction, Task>? AfterRefundIssued { get; set; }

    /// <summary>
    /// What <see cref="ChargeSavedPaymentMethodAsync"/> answers with. The default transaction id is
    /// unique per fake, as a real gateway's is: the ledger refuses a transaction it already holds.
    /// </summary>
    public CardChargeResult Charge { get; set; } = new(true, $"txn-saved-card-{Guid.NewGuid():N}");

    /// <summary>The instruction the last <see cref="CreatePaymentIntentAsync"/> call carried, or <see langword="null"/>.</summary>
    public CardPaymentIntentInstruction? LastIntentInstruction { get; private set; }

    /// <summary>The instruction the last <see cref="ChargeSavedPaymentMethodAsync"/> call carried, or <see langword="null"/> when no charge was attempted.</summary>
    public CardChargeInstruction? LastChargeInstruction { get; private set; }

    /// <summary>The payment method id <see cref="SetDefaultPaymentMethodAsync"/> was last called with, or <see langword="null"/>.</summary>
    public string? LastSetDefaultPaymentMethodId { get; private set; }

    /// <summary>The payment method id <see cref="DetachPaymentMethodAsync"/> was last called with, or <see langword="null"/>.</summary>
    public string? LastDetachedPaymentMethodId { get; private set; }

    /// <inheritdoc/>
    public Task<CardPaymentIntentResult> CreatePaymentIntentAsync(CardPaymentIntentInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        LastIntentInstruction = instruction;
        return Task.FromResult(IntentFailure is { } failure
            ? new CardPaymentIntentResult(null, failure)
            : new CardPaymentIntentResult(IntentClientSecret));
    }

    /// <summary>How many times <see cref="VerifyPaymentIntentAsync"/> was called.</summary>
    public int VerifyCalls { get; private set; }

    /// <inheritdoc/>
    public Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(CardPaymentVerifyInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        VerifyCalls++;
        return Task.FromResult(Verification);
    }

    /// <summary>
    /// The outcome of every charge the gateway took, by idempotency key. A key it has seen answers
    /// with its first outcome, a decline included, as a real gateway replays it.
    /// </summary>
    public Dictionary<string, CardChargeResult> ChargesByKey { get; } = [];

    /// <summary>
    /// When set, every <see cref="ChargeSavedPaymentMethodAsync"/> call waits here until all of the
    /// rendezvous' parties have called, so that concurrent requests have all read their invoice
    /// unpaid before any charge is answered. Null for no waiting.
    /// </summary>
    public ChargeRendezvous? ChargeRendezvous { get; set; }

    /// <inheritdoc/>
    public async Task<CardChargeResult> ChargeSavedPaymentMethodAsync(CardChargeInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        if (ChargeRendezvous is { } rendezvous)
        {
            await rendezvous.ArriveAsync(ct);
        }

        lock (ChargesByKey)
        {
            LastChargeInstruction = instruction;
            if (!ChargesByKey.TryGetValue(instruction.IdempotencyKey, out var outcome))
            {
                outcome = Charge;
                ChargesByKey[instruction.IdempotencyKey] = outcome;
                ChargeInstructionsByKey[instruction.IdempotencyKey] = instruction;
            }

            return outcome;
        }
    }

    /// <summary>The instruction of the first charge under each idempotency key, which says what it took.</summary>
    public Dictionary<string, CardChargeInstruction> ChargeInstructionsByKey { get; } = [];

    /// <summary>How many times <see cref="FindChargeAsync"/> was called.</summary>
    public int ChargeLookups { get; private set; }

    /// <inheritdoc/>
    /// <remarks>Answers with the settled charge taken under the key, never charging; a decline or an unseen key is nothing found.</remarks>
    public Task<CardChargeLookupResult> FindChargeAsync(CardChargeLookupInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        lock (ChargesByKey)
        {
            ChargeLookups++;
            if (LookupFailure is { } failure)
            {
                return Task.FromResult(CardChargeLookupResult.Failed(failure));
            }

            if (ChargesByKey.TryGetValue(instruction.IdempotencyKey, out var outcome) && outcome.Success)
            {
                var charged = ChargeInstructionsByKey[instruction.IdempotencyKey];
                return Task.FromResult(new CardChargeLookupResult(true, outcome.TransactionId, charged.Amount, charged.Currency));
            }

            return Task.FromResult(CardChargeLookupResult.None);
        }
    }

    /// <summary>Guards the customer maps below, which concurrent requests write.</summary>
    private readonly object _customerLock = new();

    /// <summary>The customers the gateway holds, by the key their creation carried. A key it has seen answers with its first customer, as a real gateway replays it.</summary>
    public Dictionary<string, string> CustomersByKey { get; } = [];

    /// <summary>How many customers the gateway really created (a replayed key does not count).</summary>
    public int CustomersCreated => CustomersByKey.Count;

    /// <summary>The customers deleted at the gateway, in order.</summary>
    public List<string> DeletedCustomers { get; } = [];

    /// <summary>The customer <see cref="FindCustomerAsync"/> finds for a client, by client id; a test adds one to stand for a customer created but never stored.</summary>
    public Dictionary<int, string> OrphanCustomers { get; } = [];

    /// <summary>The failure <see cref="CreateCustomerAsync"/> answers with instead of a customer; null to create.</summary>
    public CardGatewayFailure? CustomerFailure { get; set; }

    /// <summary>
    /// Runs after a customer is created and before the gateway answers; null for none. A test throws from it
    /// to lose the answer of a customer that does exist, or holds the request there while another one goes by.
    /// </summary>
    public Func<CardCreateCustomerInstruction, Task>? AfterCustomerCreated { get; set; }

    /// <inheritdoc/>
    public async Task<CardCustomerResult> CreateCustomerAsync(CardCreateCustomerInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        if (CustomerFailure is { } failure)
        {
            return CardCustomerResult.Failed(failure);
        }

        string customerId;
        lock (_customerLock)
        {
            if (!CustomersByKey.TryGetValue(instruction.IdempotencyKey, out customerId!))
            {
                customerId = $"cus_fake_{Guid.NewGuid():N}"[..24];
                CustomersByKey[instruction.IdempotencyKey] = customerId;
            }
        }

        if (AfterCustomerCreated is not null)
        {
            await AfterCustomerCreated(instruction);
        }

        return new CardCustomerResult(customerId);
    }

    /// <inheritdoc/>
    public Task<CardCustomerResult> FindCustomerAsync(CardFindCustomerInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        lock (_customerLock)
        {
            return Task.FromResult(OrphanCustomers.TryGetValue(instruction.ClientId, out var found)
                ? new CardCustomerResult(found)
                : CardCustomerResult.None);
        }
    }

    /// <inheritdoc/>
    public Task<CardGatewayOperationResult> DeleteCustomerAsync(CardCustomerInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        lock (_customerLock)
        {
            DeletedCustomers.Add(instruction.CustomerId);
        }

        return Task.FromResult(CardGatewayOperationResult.Succeeded);
    }

    /// <summary>The setup intent <see cref="CreateSetupIntentAsync"/> answers with.</summary>
    public CardSetupIntentResult SetupIntent { get; set; } = new("seti_test_1", "seti_test_1_secret");

    /// <summary>What <see cref="VerifySetupIntentAsync"/> answers with; a test sets the customer and card it expects.</summary>
    public CardSetupIntentStatus SetupVerification { get; set; } = new(true, null, "pm_saved_1");

    /// <summary>The instruction the last <see cref="CreateSetupIntentAsync"/> call carried, or <see langword="null"/>.</summary>
    public CardSetupIntentInstruction? LastSetupInstruction { get; private set; }

    /// <inheritdoc/>
    public Task<CardSetupIntentResult> CreateSetupIntentAsync(CardSetupIntentInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        LastSetupInstruction = instruction;
        return Task.FromResult(SetupIntent);
    }

    /// <inheritdoc/>
    public Task<CardSetupIntentStatus> VerifySetupIntentAsync(CardSetupIntentVerifyInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        return Task.FromResult(SetupVerification);
    }

    /// <inheritdoc/>
    public async Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();

        // Concurrent requests call this at once, so the shared lists are only touched under the lock.
        int callNumber;
        lock (_refundLock)
        {
            RefundInstructions.Add(instruction);
            callNumber = RefundInstructions.Count;
        }

        if (callNumber == ThrowsOnRefundCall)
        {
            throw new HttpRequestException("The card gateway did not answer the refund.");
        }

        if (RefusesRefunds)
        {
            return new CardRefundResult(false, null);
        }

        CardRefundResult refund;
        lock (_refundLock)
        {
            if (!RefundsByKey.TryGetValue(instruction.IdempotencyKey, out var known))
            {
                var issued = RefundsByKey.Count + 1;
                known = new CardRefundResult(true, issued == 1 ? RefundId : $"{RefundId}-{issued}");
                RefundsByKey[instruction.IdempotencyKey] = known;
            }

            refund = known;
        }

        if (AfterRefundIssued is not null)
        {
            await AfterRefundIssued(instruction);
        }

        return refund;
    }

    /// <inheritdoc/>
    public Task<CardPaymentMethodsResult> ListPaymentMethodsAsync(CardCustomerInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        return Task.FromResult(ListFailure is { } failure
            ? CardPaymentMethodsResult.Failed(failure)
            : new CardPaymentMethodsResult(SavedPaymentMethods));
    }

    /// <inheritdoc/>
    public Task<CardGatewayOperationResult> SetDefaultPaymentMethodAsync(CardDefaultMethodInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        if (MethodChangeFailure is { } failure)
        {
            return Task.FromResult(new CardGatewayOperationResult(failure));
        }

        LastSetDefaultPaymentMethodId = instruction.PaymentMethodId;
        return Task.FromResult(CardGatewayOperationResult.Succeeded);
    }

    /// <inheritdoc/>
    public Task<CardGatewayOperationResult> DetachPaymentMethodAsync(CardDetachInstruction instruction, CancellationToken ct)
    {
        ThrowIfNotConfigured();
        if (MethodChangeFailure is { } failure)
        {
            return Task.FromResult(new CardGatewayOperationResult(failure));
        }

        LastDetachedPaymentMethodId = instruction.PaymentMethodId;
        return Task.FromResult(CardGatewayOperationResult.Succeeded);
    }

    /// <summary>
    /// Throws when there is no secret key, which is stricter than the real adapter (it answers
    /// <see cref="CardGatewayFailure.NotConfigured"/>): a handler that reaches the gateway without
    /// checking <see cref="IsConfigured"/> first fails its test instead of passing by luck.
    /// </summary>
    /// <exception cref="ArgumentException">When <see cref="IsConfigured"/> is false.</exception>
    private void ThrowIfNotConfigured()
    {
        if (!IsConfigured)
        {
            throw new ArgumentException("The card gateway has no API key.", "ApiKey");
        }
    }
}
