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


    /// <summary>The instruction the last <see cref="CreatePaymentIntentAsync"/> call carried, or <see langword="null"/>.</summary>
    public CardPaymentIntentInstruction? LastIntentInstruction { get; private set; }




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
