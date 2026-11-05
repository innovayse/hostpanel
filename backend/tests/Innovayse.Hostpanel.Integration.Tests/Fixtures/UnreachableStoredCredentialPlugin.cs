using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A stored credential capability whose gateway cannot be reached: every call throws, the way a plugin does when the
/// gateway is down. A fake standing in for an external plugin, so a test can watch the host tolerate it.
/// </summary>
public sealed class UnreachableStoredCredentialPlugin : IStoredCredentialPlugin
{
    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <summary>Throws as an unreachable gateway does.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="HttpRequestException">Always.</exception>
    private static HttpRequestException Down() => new("The gateway is unreachable.");

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerContext context, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct) => throw Down();

    /// <inheritdoc/>
    public Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct) => throw Down();
}
