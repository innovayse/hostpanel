using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The two unsigned calls that make this installation known to the plugin registry: registering its public key and address,
/// and asking the registry to confirm the challenge this installation serves. Implemented in Infrastructure by a client that
/// only talks to the configured registry address. Every failure is a <see cref="Result"/> with a code and no detail of what
/// the registry said.
/// </summary>
public interface IRegistryInstanceApi
{
    /// <summary>Registers this installation with the registry.</summary>
    /// <param name="publicKey">The raw 32-byte Ed25519 public key, base64.</param>
    /// <param name="baseUrl">The public https address of this installation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The instance id and the challenge to serve; <c>PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED</c> when the registry already knows
    /// the key; <c>PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID</c> when the registry will not use the key or the address;
    /// <c>PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED</c> over the registry limit; or another <c>PLUGIN_REGISTRY_*</c> failure when the registry cannot be used.
    /// </returns>
    Task<Result<RegistryInstanceRegistration>> RegisterAsync(string publicKey, string baseUrl, CancellationToken ct);

    /// <summary>Asks the registry to fetch the challenge from this installation and confirm it.</summary>
    /// <param name="instanceId">The id the registry gave at registration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once verified; <c>PLUGIN_REGISTRY_INSTANCE_RESTART_NEEDED</c> when the challenge ran out of time or attempts, or the
    /// registry no longer knows the instance; <c>PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE</c> when the registry could not read the challenge;
    /// <c>PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED</c> over the registry limit; or another <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    Task<Result> VerifyAsync(Guid instanceId, CancellationToken ct);
}
