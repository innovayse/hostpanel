using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Queries.GetIntegration;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one way the host reaches the stored credential capability of a payment gateway module. It resolves the
/// capability, calls it, and turns a plugin that throws into a handled error, so a handler never meets a gateway
/// exception and never has to guess which calls may have moved money.
/// </summary>
/// <remarks>
/// A plugin throws only when the outcome of a call is unknown (the gateway was not reached or did not answer), so
/// every exception becomes <see cref="HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE"/>. For
/// <see cref="ChargeAsync"/> that code means "the money may or may not have moved": the caller must keep the claim
/// open and settle it through <see cref="FindChargeAsync"/>.
/// </remarks>
public sealed class StoredCredentialGateway
{
    /// <summary>Resolves the capability of a module.</summary>
    private readonly IStoredCredentialPluginResolver _resolver;

    /// <summary>Reads whether the built-in gateway is switched on in the admin integrations.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Logs the exceptions turned into handled errors.</summary>
    private readonly ILogger<StoredCredentialGateway> _logger;

    /// <summary>Initializes the gateway.</summary>
    /// <param name="resolver">Resolves the capability of a module.</param>
    /// <param name="bus">Reads whether the built-in gateway is switched on in the admin integrations.</param>
    /// <param name="logger">Logs the exceptions turned into handled errors.</param>
    public StoredCredentialGateway(IStoredCredentialPluginResolver resolver, IMessageBus bus, ILogger<StoredCredentialGateway> logger)
    {
        _resolver = resolver;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Tells whether a module offers saved methods.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the module resolves and implements the capability.</returns>
    public async Task<bool> IsOfferedAsync(string module, CancellationToken ct) =>
        await ResolveEnabledAsync(module, ct) is not null;

    /// <summary>Finds or opens the client's account at the gateway.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="context">Who the account is for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway's customer reference, or the error.</returns>
    public Task<Result<CredentialCustomerResult>> EnsureCustomerAsync(string module, CredentialCustomerContext context, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.EnsureCustomerAsync(context, ct), ct);

    /// <summary>Starts saving a method.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="context">The customer and where the payer returns to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The save session, or the error.</returns>
    public Task<Result<SaveMethodSession>> BeginSaveMethodAsync(string module, SaveMethodContext context, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.BeginSaveMethodAsync(context, ct), ct);

    /// <summary>Finishes a save session.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="sessionId">The session id <see cref="BeginSaveMethodAsync"/> answered with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether a method was saved and which, or the error.</returns>
    public Task<Result<SavedMethodResult>> CompleteSaveMethodAsync(string module, string sessionId, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.CompleteSaveMethodAsync(sessionId, ct), ct);

    /// <summary>Charges a saved method.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="instruction">What to charge, to whom, under which idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome, or <see cref="HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE"/> when it is not known.</returns>
    public Task<Result<StoredChargeResult>> ChargeAsync(string module, StoredChargeInstruction instruction, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.ChargeAsync(instruction, ct), ct);

    /// <summary>Looks up a charge by the idempotency key it was sent with.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="customerRef">The customer the charge was made for.</param>
    /// <param name="idempotencyKey">The key the charge was sent with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the gateway knows about the key, or the error when the lookup itself failed.</returns>
    public Task<Result<StoredChargeLookup>> FindChargeAsync(string module, string customerRef, string idempotencyKey, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.FindChargeAsync(customerRef, idempotencyKey, ct), ct);

    /// <summary>Lists a customer's saved methods.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="customerRef">The customer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved methods, or the error.</returns>
    public Task<Result<IReadOnlyList<SavedMethod>>> ListMethodsAsync(string module, string customerRef, CancellationToken ct) =>
        CallAsync(module, plugin => plugin.ListMethodsAsync(customerRef, ct), ct);

    /// <summary>Makes a saved method the customer's default.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="customerRef">The customer.</param>
    /// <param name="methodRef">The method to make the default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the error.</returns>
    public async Task<Result> SetDefaultMethodAsync(string module, string customerRef, string methodRef, CancellationToken ct) =>
        await CallAsync<bool>(module, async plugin =>
        {
            await plugin.SetDefaultMethodAsync(customerRef, methodRef, ct);
            return true;
        }, ct) is { IsFailure: true } failed
            ? Result.FailureCode(failed.ErrorCode!)
            : Result.Success();

    /// <summary>Removes a saved method.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="customerRef">The customer.</param>
    /// <param name="methodRef">The method to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the error.</returns>
    public async Task<Result> RemoveMethodAsync(string module, string customerRef, string methodRef, CancellationToken ct) =>
        await CallAsync<bool>(module, async plugin =>
        {
            await plugin.RemoveMethodAsync(customerRef, methodRef, ct);
            return true;
        }, ct) is { IsFailure: true } failed
            ? Result.FailureCode(failed.ErrorCode!)
            : Result.Success();

    /// <summary>Resolves the capability and runs one call against it.</summary>
    /// <typeparam name="T">What the call answers.</typeparam>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="call">The call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The answer, <see cref="HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE"/> when the module offers no saved methods, or <see cref="HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE"/> when the plugin threw.</returns>
    private async Task<Result<T>> CallAsync<T>(string module, Func<IStoredCredentialPlugin, Task<T>> call, CancellationToken ct)
    {
        var plugin = await ResolveEnabledAsync(module, ct);
        if (plugin is null)
        {
            return Result<T>.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        }

        try
        {
            return Result.Success(await call(plugin));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The plugin's contract: an exception means the outcome is unknown, never "refused".
            _logger.LogWarning(ex, "The stored credential capability of module {Module} did not complete a call.", module);
            return Result<T>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }
    }

    /// <summary>Resolves the capability of a module that is switched on.</summary>
    /// <param name="module">The payment gateway module id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The capability, or <see langword="null"/> when the module offers none. A payment plugin passes the gates of its
    /// resolver (installed, enabled, configured); the built-in card gateway is also checked against its switch on the
    /// admin integrations page, so an operator who turned it off stops its saved cards being charged too.
    /// </returns>
    private async Task<IStoredCredentialPlugin?> ResolveEnabledAsync(string module, CancellationToken ct)
    {
        if (module == BuiltInPaymentModules.Stripe)
        {
            var integration = await _bus.InvokeAsync<Result<IntegrationDetailDto>>(
                new GetIntegrationQuery(BuiltInPaymentModules.StripeIntegrationSlug), ct);
            if (integration.Value?.IsEnabled != true)
            {
                return null;
            }
        }

        return await _resolver.ResolveAsync(module, ct);
    }
}
