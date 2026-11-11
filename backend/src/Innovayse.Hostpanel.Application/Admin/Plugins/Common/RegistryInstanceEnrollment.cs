using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Makes this installation known to the plugin registry, once and then only when needed: generates the key pair, registers its
/// public half with the public address of the installation, lets the registry fetch the challenge from the well-known address
/// and confirm it. The one place that logic lives, so the startup job, the page's retry button and a request for a restricted
/// plugin all do exactly the same, and doing it twice does no harm: a verified identity answers at once, a failed attempt
/// waits out its back-off unless the caller forces it.
/// </summary>
/// <remarks>
/// The registry refuses to register one key twice, so when it asks for a fresh start (the challenge ran out of time or
/// attempts, it forgot the instance, or it already knows the key) the pair is generated again. Nothing is saved while the
/// registry is switched off, and nothing at all when the configured address cannot be used: both are answered with their code.
/// </remarks>
public sealed class RegistryInstanceEnrollment
{
    /// <summary>The most times one run starts over with a new key pair: the second start failing the same way is a failure.</summary>
    private const int MaxStarts = 2;

    /// <summary>
    /// Admits one enrolment at a time in this process, so the startup job and a request that arrive together register once. It is
    /// static because the service is scoped; another process is not covered, and the registry's refusal of a second registration
    /// of one key ends that case with a fresh start rather than with two live identities.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Reads and writes the identity.</summary>
    private readonly RegistryInstanceStore _store;

    /// <summary>The unsigned register and verify calls to the registry.</summary>
    private readonly IRegistryInstanceApi _api;

    /// <summary>Generates the key pair.</summary>
    private readonly IRegistryInstanceKeys _keys;

    /// <summary>The clock back-offs are judged against.</summary>
    private readonly IClock _clock;

    /// <summary>Where the client portal, which is the public address of this installation, is reachable.</summary>
    private readonly ClientPortalOptions _portal;

    /// <summary>Words the code of the last failure.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Logs what happened, by code only.</summary>
    private readonly ILogger<RegistryInstanceEnrollment> _logger;

    /// <summary>Initializes the service.</summary>
    /// <param name="store">Reads and writes the identity.</param>
    /// <param name="api">The unsigned register and verify calls to the registry.</param>
    /// <param name="keys">Generates the key pair.</param>
    /// <param name="clock">The clock back-offs are judged against.</param>
    /// <param name="portal">Where the client portal, which is the public address of this installation, is reachable.</param>
    /// <param name="messages">Words the code of the last failure.</param>
    /// <param name="logger">Logs what happened, by code only.</param>
    public RegistryInstanceEnrollment(
        RegistryInstanceStore store,
        IRegistryInstanceApi api,
        IRegistryInstanceKeys keys,
        IClock clock,
        IOptions<ClientPortalOptions> portal,
        IErrorMessageResolver messages,
        ILogger<RegistryInstanceEnrollment> logger)
    {
        _store = store;
        _api = api;
        _keys = keys;
        _clock = clock;
        _portal = portal.Value;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Brings the identity to Verified when it is not, or says why it cannot be now. A verified identity answers without a call.
    /// </summary>
    /// <param name="force">
    /// <see langword="true"/> to ignore the back-off and to register again after a revocation (the page's retry button);
    /// <see langword="false"/> for the startup job and for a request for a restricted plugin, which respect both.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the identity is verified; <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off;
    /// <c>PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID</c> when the configured address cannot be told to the registry;
    /// <c>PLUGIN_REGISTRY_INSTANCE_REVOKED</c> for a revoked identity that was not forced; the code of the last failure while its
    /// back-off runs; or the code of this attempt's failure.
    /// </returns>
    public async Task<Result> EnsureVerifiedAsync(bool force, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await RunAsync(force, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Reads where the registration stands, in the caller's language.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state, the instance id and the last failure with its sentence; nothing secret.</returns>
    public async Task<RegistryInstanceDto> GetStatusAsync(CancellationToken ct)
    {
        var record = await _store.LoadAsync(ct);
        if (record is null)
        {
            // No identity exists yet. An address the registry cannot use is the one thing worth telling already.
            return RegistryInstanceAddress.TryNormalize(_portal.BaseUrl, out _)
                ? new RegistryInstanceDto(RegistryInstanceState.NotRegistered, null, null, null, null, null, null)
                : new RegistryInstanceDto(
                    RegistryInstanceState.Error,
                    null,
                    null,
                    HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID,
                    _messages.Resolve(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID),
                    null,
                    null);
        }

        return new RegistryInstanceDto(
            record.State,
            record.InstanceId,
            record.BaseUrl,
            record.ErrorCode,
            record.ErrorCode is null ? null : _messages.Resolve(record.ErrorCode),
            record.VerifiedAt,
            record.NextAttemptAt);
    }

    /// <summary>One enrolment, under the gate.</summary>
    /// <param name="force">Whether the back-off and a revocation are ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>As <see cref="EnsureVerifiedAsync"/>.</returns>
    private async Task<Result> RunAsync(bool force, CancellationToken ct)
    {
        var record = await _store.LoadAsync(ct);
        if (record?.State == RegistryInstanceState.Verified)
        {
            return Result.Success();
        }

        if (!RegistryInstanceAddress.TryNormalize(_portal.BaseUrl, out var address))
        {
            return Result.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID);
        }

        if (record?.State == RegistryInstanceState.Revoked && !force)
        {
            return Result.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED);
        }

        if (record is not null && !force && !record.IsDue(_clock.UtcNow))
        {
            return Result.FailureCode(record.ErrorCode ?? HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED);
        }

        record = Prepare(record, address, force);
        for (var start = 1; ; start++)
        {
            var outcome = await AttemptAsync(record, ct);
            if (outcome.IsSuccess || outcome.ErrorCode != HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED)
            {
                return outcome;
            }

            // The registry asked for a fresh start: a new pair, then the same two calls once more.
            if (start >= MaxStarts)
            {
                return await FailAsync(record, outcome.ErrorCode!, ct);
            }

            record.Restart(_keys.Generate(), address);
        }
    }

    /// <summary>Gives the record this run works on: the one stored, or a new one; a record that cannot go on is restarted with a new pair.</summary>
    /// <param name="record">The stored identity, or <see langword="null"/>.</param>
    /// <param name="address">The configured public address, normalised.</param>
    /// <param name="force">Whether a revoked identity is registered again.</param>
    /// <returns>The record to work on.</returns>
    private RegistryInstanceRecord Prepare(RegistryInstanceRecord? record, string address, bool force)
    {
        if (record is null)
        {
            return RegistryInstanceRecord.Create(_keys.Generate(), address);
        }

        // A registration for another address cannot be confirmed from this one, and a revoked identity is only reused on purpose.
        if (!string.Equals(record.BaseUrl, address, StringComparison.Ordinal) || (record.State == RegistryInstanceState.Revoked && force))
        {
            record.Restart(_keys.Generate(), address);
        }

        record.ClearPause();
        return record;
    }

    /// <summary>Registers when there is no instance id yet, then asks the registry to verify, and saves where that left the identity.</summary>
    /// <param name="record">The identity being worked on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once verified; a failure code, <c>PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED</c> being the registry asking for a fresh start (not yet saved as a failure).</returns>
    private async Task<Result> AttemptAsync(RegistryInstanceRecord record, CancellationToken ct)
    {
        if (record.InstanceId is null)
        {
            var registered = await _api.RegisterAsync(record.PublicKey, record.BaseUrl, ct);
            if (registered.IsFailure)
            {
                return registered.ErrorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED
                    ? Result.FailureCode(registered.ErrorCode)
                    : await FailAsync(record, registered.ErrorCode!, ct);
            }

            record.Register(registered.Value!);

            // Saved before the verification is asked for: the registry fetches the challenge from this very installation, and the
            // well-known address answers from the saved identity.
            await _store.SaveAsync(record, ct);
        }

        var verified = await _api.VerifyAsync(record.InstanceId!.Value, ct);
        if (verified.IsFailure)
        {
            return verified.ErrorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED
                ? Result.FailureCode(verified.ErrorCode)
                : await FailAsync(record, verified.ErrorCode!, ct);
        }

        record.Verify(_clock.UtcNow);
        await _store.SaveAsync(record, ct);
        _logger.LogInformation("This installation is verified by the plugin registry.");
        return Result.Success();
    }

    /// <summary>Saves a failed attempt with its back-off, unless the failure says nothing about the identity.</summary>
    /// <param name="record">The identity being worked on.</param>
    /// <param name="code">The failure code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The failure.</returns>
    private async Task<Result> FailAsync(RegistryInstanceRecord record, string code, CancellationToken ct)
    {
        // A switched-off registry is not a failure of the registration: nothing is saved, so switching it on starts clean.
        if (code != HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED)
        {
            record.Fail(code, _clock.UtcNow);
            await _store.SaveAsync(record, ct);
            _logger.LogWarning("The registration with the plugin registry failed: {ErrorCode}.", code);
        }

        return Result.FailureCode(code);
    }
}
