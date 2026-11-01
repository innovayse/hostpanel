using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Payment plugin that also opts in to <see cref="IStoredCredentialPlugin"/>. The resolver tests only need the
/// capability to be present, so no operation does anything.
/// </summary>
internal sealed class StoredCredentialCapablePaymentPlugin : IPaymentPlugin, IStoredCredentialPlugin
{
    /// <summary>Initializes the fake; the composed configuration is accepted because the activator always passes it.</summary>
    /// <param name="configuration">The configuration the activator composed.</param>
    public StoredCredentialCapablePaymentPlugin(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>Gets the configuration the activator passed in.</summary>
    public IConfiguration Configuration { get; }

    /// <inheritdoc/>
    public string CurrencyCode => "840";

    /// <inheritdoc/>
    public Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerDetails details, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct)
        => throw new NotSupportedException();
}
