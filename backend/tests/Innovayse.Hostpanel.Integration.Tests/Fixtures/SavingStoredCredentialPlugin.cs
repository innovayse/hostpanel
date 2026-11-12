using System.Globalization;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A payment plugin that also offers stored credentials and honours <see cref="PaymentInstruction.SavePaymentMethod"/>: it
/// records every instruction and customer request it receives, reads every registered payment as paid for the amount it
/// was registered with, and reports <see cref="MethodOnStatus"/> as the method the gateway stored. A fake port standing in
/// for a hosted gateway, which a test must never actually call.
/// </summary>
public sealed class SavingStoredCredentialPlugin : IPaymentPlugin, IStoredCredentialPlugin
{
    /// <summary>The amount each registered gateway order was registered for, in minor units.</summary>
    private readonly Dictionary<string, long> _amounts = [];

    /// <summary>Source of the numbers that make gateway order ids unique.</summary>
    private int _orders;

    /// <summary>Gets every instruction the host registered a payment with, in order.</summary>
    public List<PaymentInstruction> Instructions { get; } = [];

    /// <summary>Gets every customer the host asked for, in order.</summary>
    public List<CredentialCustomerContext> Customers { get; } = [];

    /// <summary>Gets or sets the method the gateway reports as stored on a paid payment; <see langword="null"/> reports none.</summary>
    public SavedMethod? MethodOnStatus { get; set; }

    /// <summary>What completing a save session answers; completing one without setting it is a test mistake.</summary>
    public SavedMethodResult? SaveSessionResult { get; set; }

    /// <summary>Gets or sets an exception <see cref="EnsureCustomerAsync"/> throws instead of answering, as a gateway outage would, when set.</summary>
    public Exception? EnsureCustomerThrows { get; set; }

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)
    {
        Instructions.Add(instruction);
        var orderId = string.Create(CultureInfo.InvariantCulture, $"saving-{Guid.NewGuid():N}-{++_orders}");
        _amounts[orderId] = instruction.AmountMinor;
        return Task.FromResult(new PaymentSession(orderId, $"{instruction.ReturnUrl}?order={orderId}"));
    }

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct) =>
        Task.FromResult(new GatewayPaymentStatus(
            GatewayPaymentState.Paid, $"tx-{gatewayOrderId}", "paid", _amounts[gatewayOrderId], SavedMethod: MethodOnStatus));

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct) =>
        throw new NotSupportedException("No test exercising this fake needs a refund.");

    /// <inheritdoc/>
    public Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerContext context, CancellationToken ct)
    {
        Customers.Add(context);
        return EnsureCustomerThrows is not null
            ? Task.FromException<CredentialCustomerResult>(EnsureCustomerThrows)
            : Task.FromResult(new CredentialCustomerResult($"saving-cus-{context.ClientReference}"));
    }

    /// <inheritdoc/>
    public Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct) =>
        Task.FromResult(new SaveMethodSession(
            string.Create(CultureInfo.InvariantCulture, $"saving-session-{Guid.NewGuid():N}"), SaveMethodMode.Redirect, null, context.ReturnUrl));

    /// <inheritdoc/>
    public Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct) =>
        SaveSessionResult is { } result
            ? Task.FromResult(result)
            : throw new NotSupportedException("Set SaveSessionResult before a save session is completed.");

    /// <inheritdoc/>
    public Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SavedMethod>>(MethodOnStatus is null ? [] : [MethodOnStatus]);

    /// <inheritdoc/>
    public Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct) =>
        throw new NotSupportedException();
}
