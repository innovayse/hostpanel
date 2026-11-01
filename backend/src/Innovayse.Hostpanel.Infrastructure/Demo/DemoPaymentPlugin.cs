using System.Globalization;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Hosted-page payment gateway for the public demo. The "hosted page" is the shop's own return
/// address: the customer is sent straight back, and the session then reads as paid, so the whole
/// redirect flow can be walked without a bank. The session id carries the amount it was opened
/// for, so the paid status reports what was taken the way a real gateway does.
/// </summary>
/// <remarks>
/// It also implements <see cref="IStoredCredentialPlugin"/> in memory, so every path of the capability, the key
/// lookup included, can be walked without a gateway. A stored charge is decided by the last two digits of its
/// amount in minor units (the amount modulo 100), so a test picks the outcome by picking the amount:
/// <list type="table">
/// <item><term>02</term><description>Declined.</description></item>
/// <item><term>03</term><description>Action required; the action URL is the return URL with <c>demo=action</c>.</description></item>
/// <item><term>04</term><description>Pending; the lookup keeps answering pending.</description></item>
/// <item><term>05</term><description>Unknown outcome after the money was taken: the charge is recorded as succeeded, then <see cref="TimeoutException"/> is thrown.</description></item>
/// <item><term>06</term><description>Unknown outcome before anything was taken: nothing is recorded, then <see cref="TimeoutException"/> is thrown.</description></item>
/// <item><term>any other</term><description>Succeeded, with transaction id <c>demo-charge-{idempotencyKey}</c>.</description></item>
/// </list>
/// The first result for an idempotency key is final: a repeated charge with the same key answers the recorded
/// result, never charges again and never throws. Saving a method stores a demo Visa ending in 4242.
/// </remarks>
internal sealed class DemoPaymentPlugin : IPaymentPlugin, IStoredCredentialPlugin
{
    /// <summary>Last two digits of an amount that make the stored charge decline.</summary>
    internal const long DeclinedSuffix = 2;

    /// <summary>Last two digits of an amount that make the stored charge ask the payer to authenticate.</summary>
    internal const long ActionRequiredSuffix = 3;

    /// <summary>Last two digits of an amount that leave the stored charge pending.</summary>
    internal const long PendingSuffix = 4;

    /// <summary>Last two digits of an amount that take the money and then fail to answer.</summary>
    internal const long TimeoutAfterChargeSuffix = 5;

    /// <summary>Last two digits of an amount that fail to answer before taking anything.</summary>
    internal const long TimeoutBeforeChargeSuffix = 6;

    /// <summary>The longest idempotency key the capability allows.</summary>
    private const int MaxKeyLength = 64;

    /// <summary>Every ISO 4217 numeric code the fake gateway settles in.</summary>
    private readonly IReadOnlySet<string> _numericCodes;

    /// <summary>The stored methods and charges; shared by every instance, because the resolver builds a new plugin per call.</summary>
    private readonly DemoStoredCredentialState _state;

    /// <summary>Creates the gateway for the currencies the shop sells in.</summary>
    /// <param name="baseNumericCode">Numeric code reported as the gateway's own currency (the shop's base currency).</param>
    /// <param name="numericCodes">Numeric codes of every currency the gateway accepts; read from the seeded currencies.</param>
    /// <param name="state">The stored methods and charges to share; a private book when null, so a lone instance still works.</param>
    public DemoPaymentPlugin(string baseNumericCode, IEnumerable<string> numericCodes, DemoStoredCredentialState? state = null)
    {
        CurrencyCode = baseNumericCode;
        _numericCodes = new HashSet<string>(numericCodes, StringComparer.Ordinal) { baseNumericCode };
        _state = state ?? new DemoStoredCredentialState();
    }

    /// <inheritdoc/>
    public string CurrencyCode { get; }

    /// <inheritdoc/>
    public bool AcceptsCurrency(string currencyNumeric) => _numericCodes.Contains(currencyNumeric);

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct)
    {
        var separator = request.ReturnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var gatewayOrderId = string.Create(CultureInfo.InvariantCulture, $"demo-{request.OrderNumber}-{request.AmountMinor}");

        return Task.FromResult(new PaymentSession(gatewayOrderId, $"{request.ReturnUrl}{separator}demo=1"));
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gatewayOrderId);

        // The amount is the last segment of the session id; one without it reports no amount.
        long? amountMinor = long.TryParse(
            gatewayOrderId[(gatewayOrderId.LastIndexOf('-') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
        return Task.FromResult(new GatewayPaymentStatus(
            GatewayPaymentState.Paid, $"demo-txn-{gatewayOrderId}", "demo:paid", amountMinor));
    }

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct) =>
        Task.FromResult($"demo-refund-{gatewayOrderId}");

    /// <inheritdoc/>
    public Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerDetails details, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(details);
        return Task.FromResult(new CredentialCustomerResult($"demo-cus-{details.ClientReference}"));
    }

    /// <inheritdoc/>
    public Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sessionId = _state.OpenSession(context.CustomerRef);
        return Task.FromResult(new SaveMethodSession(sessionId, SaveMethodMode.EmbeddedToken, $"demo-token-{sessionId}", null));
    }

    /// <inheritdoc/>
    public Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var method = _state.CompleteSession(sessionId);
        return Task.FromResult(new SavedMethodResult(method is not null, method));
    }

    /// <inheritdoc/>
    public Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ValidateKey(instruction.IdempotencyKey);

        // A key that already charged answers what it answered the first time, whatever the amount says now.
        var earlier = _state.Find(instruction.CustomerRef, instruction.IdempotencyKey);
        if (earlier is not null)
        {
            return Task.FromResult(earlier);
        }

        var suffix = instruction.AmountMinor % 100;
        if (suffix == TimeoutBeforeChargeSuffix)
        {
            throw new TimeoutException("The demo gateway did not answer.");
        }

        var result = suffix switch
        {
            DeclinedSuffix => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null),
            ActionRequiredSuffix => new StoredChargeResult(
                StoredChargeOutcome.ActionRequired, null, null, null, ActionUrl(instruction.ReturnUrl)),
            PendingSuffix => new StoredChargeResult(StoredChargeOutcome.Pending, null, null, null, null),
            _ => new StoredChargeResult(
                StoredChargeOutcome.Succeeded, $"demo-charge-{instruction.IdempotencyKey}", instruction.AmountMinor, instruction.Currency, null),
        };

        // Record before answering, so a charge whose answer is lost is still found by its key.
        var recorded = _state.Record(instruction.CustomerRef, instruction.IdempotencyKey, result);
        if (suffix == TimeoutAfterChargeSuffix)
        {
            throw new TimeoutException("The demo gateway did not answer.");
        }

        return Task.FromResult(recorded);
    }

    /// <inheritdoc/>
    public Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(customerRef);
        ValidateKey(idempotencyKey);

        var charge = _state.Find(customerRef, idempotencyKey);
        return Task.FromResult(charge is null
            ? new StoredChargeLookup(false, null, null, null, null, null)
            : new StoredChargeLookup(true, charge.Outcome, charge.TransactionId, charge.AmountMinor, charge.Currency, charge.ActionUrl));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(customerRef);
        return Task.FromResult(_state.Methods(customerRef));
    }

    /// <inheritdoc/>
    public Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        _state.SetDefault(customerRef, methodRef);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        _state.Remove(customerRef, methodRef);
        return Task.CompletedTask;
    }

    /// <summary>Refuses a key that breaks the capability's rule: opaque ASCII, one to 64 characters.</summary>
    /// <param name="idempotencyKey">The key to check.</param>
    /// <exception cref="ArgumentException">The key is empty, longer than 64 characters or not ASCII.</exception>
    private static void ValidateKey(string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        if (idempotencyKey.Length is 0 or > MaxKeyLength || !idempotencyKey.All(char.IsAscii))
        {
            throw new ArgumentException("The idempotency key must be 1 to 64 ASCII characters.", nameof(idempotencyKey));
        }
    }

    /// <summary>Builds the address the demo sends a payer to for authentication: the return address with a marker.</summary>
    /// <param name="returnUrl">The return address from the charge, or null.</param>
    /// <returns>The action URL.</returns>
    private static string ActionUrl(string? returnUrl)
    {
        var baseUrl = string.IsNullOrEmpty(returnUrl) ? "https://demo.invalid/authenticate" : returnUrl;
        return $"{baseUrl}{(baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}demo=action";
    }
}
