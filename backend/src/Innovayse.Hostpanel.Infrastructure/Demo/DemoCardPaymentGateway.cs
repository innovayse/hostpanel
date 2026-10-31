using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Card gateway for the public demo: reports itself configured, settles every payment intent it opened and
/// holds three fixed sample methods per customer (two cards and a bank account) in the shape a real
/// gateway reports them. Only the last four digits ever exist, and test-style ones. Setting a default
/// or removing a method is remembered (in <see cref="DemoCardState"/>) until the demo reset so the demo list reacts to it.
/// Identifiers are derived from the input, never random, and nothing leaves the process.
/// </summary>
public sealed class DemoCardPaymentGateway : ICardPaymentGateway
{
    /// <summary>The identifier prefix of the sample Visa card; the customer id follows it, so an id names its owner.</summary>
    private const string VisaCardId = "demo-card-visa:";

    /// <summary>The identifier prefix of the sample Mastercard.</summary>
    private const string MastercardId = "demo-card-mastercard:";

    /// <summary>The identifier prefix of the sample bank account.</summary>
    private const string BankAccountId = "demo-bank-account:";

    /// <summary>Years from now the Visa card expires, so it never reads as expired.</summary>
    private const int VisaExpiryYearsAhead = 3;

    /// <summary>Years from now the Mastercard expires.</summary>
    private const int MastercardExpiryYearsAhead = 2;

    /// <summary>What visitors changed on the sample methods; cleared by the demo reset.</summary>
    private readonly DemoCardState _state;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Creates the gateway.</summary>
    /// <param name="state">What visitors changed on the sample methods.</param>
    /// <param name="clock">The source of the current moment.</param>
    public DemoCardPaymentGateway(DemoCardState state, IClock clock)
    {
        _state = state;
        _clock = clock;
    }

    /// <inheritdoc/>
    public bool IsConfigured => true;

    /// <inheritdoc/>
    /// <remarks>The intent is remembered (in <see cref="DemoCardState"/>) as settled, so verifying it reports what it was opened for.</remarks>
    public Task<CardPaymentIntentResult> CreatePaymentIntentAsync(
        CardPaymentIntentInstruction instruction, CancellationToken ct)
    {
        var currency = instruction.Currency.ToLowerInvariant();
        var intentId = $"demo-intent-{instruction.InvoiceId}-{currency}";

        // A payment that agreed to keep the card leaves a new card on the customer, like the real gateway does.
        var savedMethodId = instruction.SaveCard && instruction.CustomerId is { } customerId
            ? SaveNewCard(customerId)
            : null;
        _state.OpenIntent(intentId, new CardPaymentIntentStatus(
            true,
            $"demo-txn-{intentId}",
            CurrencyCode.ToMinorUnits(instruction.Amount, instruction.Currency),
            currency,
            instruction.OrderId,
            instruction.InvoiceId,
            PaymentMethodId: savedMethodId));
        return Task.FromResult(new CardPaymentIntentResult(intentId, IntentId: intentId));
    }

    /// <inheritdoc/>
    /// <remarks>An intent the demo never opened reads as unsettled, for no amount.</remarks>
    public Task<CardPaymentIntentStatus> VerifyPaymentIntentAsync(CardPaymentVerifyInstruction instruction, CancellationToken ct) =>
        Task.FromResult(_state.FindIntent(instruction.PaymentIntentId)
            ?? new CardPaymentIntentStatus(false, null, 0, string.Empty, null, null));

    /// <inheritdoc/>
    /// <remarks>
    /// Always settles, with an id derived from the invoice so the same invoice yields the same id; no money moves.
    /// The charge is remembered (in <see cref="DemoCardState"/>) by its idempotency key, so a lookup finds it.
    /// </remarks>
    public Task<CardChargeResult> ChargeSavedPaymentMethodAsync(CardChargeInstruction instruction, CancellationToken ct)
    {
        var transactionId = $"demo-charge-{instruction.InvoiceId}";
        _state.RecordCharge(
            instruction.IdempotencyKey,
            new CardChargeLookupResult(true, transactionId, instruction.Amount, instruction.Currency));
        return Task.FromResult(new CardChargeResult(true, transactionId));
    }

    /// <inheritdoc/>
    /// <remarks>Answers with a charge the demo took under the key, or that nothing was found.</remarks>
    public Task<CardChargeLookupResult> FindChargeAsync(CardChargeLookupInstruction instruction, CancellationToken ct) =>
        Task.FromResult(_state.FindCharge(instruction.IdempotencyKey) ?? CardChargeLookupResult.None);

    /// <inheritdoc/>
    /// <remarks>The customer id is derived from the client, so the same client always gets the same one.</remarks>
    public Task<CardCustomerResult> CreateCustomerAsync(CardCreateCustomerInstruction instruction, CancellationToken ct) =>
        Task.FromResult(new CardCustomerResult($"demo-customer-{instruction.ClientId}"));

    /// <inheritdoc/>
    /// <remarks>The demo opens a customer only when asked, so there is never an orphan to find.</remarks>
    public Task<CardCustomerResult> FindCustomerAsync(CardFindCustomerInstruction instruction, CancellationToken ct) =>
        Task.FromResult(CardCustomerResult.None);

    /// <inheritdoc/>
    public Task<CardGatewayOperationResult> DeleteCustomerAsync(CardCustomerInstruction instruction, CancellationToken ct) =>
        Task.FromResult(CardGatewayOperationResult.Succeeded);

    /// <inheritdoc/>
    /// <remarks>
    /// The demo has no card form to fill in, so opening a setup intent already saves a test card and the
    /// intent reads back as succeeded. The same idempotency key answers with the same intent and the same card.
    /// </remarks>
    public Task<CardSetupIntentResult> CreateSetupIntentAsync(CardSetupIntentInstruction instruction, CancellationToken ct)
    {
        var setupIntentId = $"demo-seti-{instruction.IdempotencyKey}";
        if (_state.FindSetupIntent(setupIntentId) is null)
        {
            var methodId = SaveNewCard(instruction.CustomerId);
            _state.OpenSetupIntent(setupIntentId, new CardSetupIntentStatus(true, instruction.CustomerId, methodId));
        }

        return Task.FromResult(new CardSetupIntentResult(setupIntentId, $"{setupIntentId}_secret"));
    }

    /// <inheritdoc/>
    public Task<CardSetupIntentStatus> VerifySetupIntentAsync(CardSetupIntentVerifyInstruction instruction, CancellationToken ct) =>
        Task.FromResult(_state.FindSetupIntent(instruction.SetupIntentId) ?? new CardSetupIntentStatus(false, null, null));

    /// <summary>Saves a test card against a customer, as a real gateway would after a card was entered.</summary>
    /// <param name="customerId">The customer the card is saved against.</param>
    /// <returns>The id of the new payment method.</returns>
    private string SaveNewCard(string customerId)
    {
        var number = _state.AddedMethods(customerId).Count + 1;
        var id = $"demo-card-added-{number}:{customerId}";
        _state.AddMethod(customerId, new CardPaymentMethodDto(
            id, SavedPaymentMethodType.Card, "visa", "4242", 12, _clock.UtcNow.Year + VisaExpiryYearsAhead, false));
        return id;
    }

    /// <inheritdoc/>
    public Task<CardRefundResult> RefundAsync(CardRefundInstruction instruction, CancellationToken ct) =>
        Task.FromResult(new CardRefundResult(true, $"demo-refund-{instruction.TransactionId}"));

    /// <inheritdoc/>
    public Task<CardPaymentMethodsResult> ListPaymentMethodsAsync(CardCustomerInstruction instruction, CancellationToken ct)
    {
        var customerId = instruction.CustomerId;
        var year = _clock.UtcNow.Year;
        (string Id, SavedPaymentMethodType Type, string Brand, string Last4, int? Month, int? Year)[] samples =
        [
            ($"{VisaCardId}{customerId}", SavedPaymentMethodType.Card, "visa", "4242", 12, year + VisaExpiryYearsAhead),
            ($"{MastercardId}{customerId}", SavedPaymentMethodType.Card, "mastercard", "4444", 8, year + MastercardExpiryYearsAhead),
            ($"{BankAccountId}{customerId}", SavedPaymentMethodType.BankAccount, "Demo Bank", "6789", null, null),
        ];

        // The cards visitors added come after the three samples, in the order they were added.
        var added = _state.AddedMethods(customerId)
            .Select(m => (m.Id, m.Type, Brand: m.Brand ?? string.Empty, Last4: m.Last4 ?? string.Empty, Month: m.ExpiryMonth, Year: m.ExpiryYear));
        var remaining = samples.Concat(added).Where(s => !_state.IsRemoved(s.Id)).ToList();

        // The chosen default wins while it still exists; otherwise the first remaining method is the default.
        var defaultId = _state.TryGetDefault(customerId, out var chosen) && remaining.Any(s => s.Id == chosen)
            ? chosen
            : remaining.FirstOrDefault().Id;

        IReadOnlyList<CardPaymentMethodDto> methods = remaining
            .Select(s => new CardPaymentMethodDto(s.Id, s.Type, s.Brand, s.Last4, s.Month, s.Year, s.Id == defaultId))
            .ToList();

        return Task.FromResult(new CardPaymentMethodsResult(methods));
    }

    /// <inheritdoc/>
    public Task<CardGatewayOperationResult> SetDefaultPaymentMethodAsync(CardDefaultMethodInstruction instruction, CancellationToken ct)
    {
        _state.ChooseDefault(instruction.CustomerId, instruction.PaymentMethodId);
        return Task.FromResult(CardGatewayOperationResult.Succeeded);
    }

    /// <inheritdoc/>
    /// <remarks>Remembered by method id in the shared state, which the demo reset clears.</remarks>
    public Task<CardGatewayOperationResult> DetachPaymentMethodAsync(CardDetachInstruction instruction, CancellationToken ct)
    {
        _state.Remove(instruction.PaymentMethodId);
        return Task.FromResult(CardGatewayOperationResult.Succeeded);
    }
}
