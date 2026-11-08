using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Signs the calls this installation makes to the plugin registry for a restricted plugin, with the private key of its
/// registry identity. The key never leaves the implementation: a caller gets the four header values and nothing else.
/// </summary>
public interface IRegistryRequestSigner
{
    /// <summary>Signs one request.</summary>
    /// <param name="method">The HTTP method, upper case.</param>
    /// <param name="target">The request target exactly as it will be sent: the path and the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The header values, or <c>PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED</c> or <c>PLUGIN_REGISTRY_INSTANCE_REVOKED</c> when this installation has no verified identity.</returns>
    Task<Result<RegistryRequestSignature>> SignAsync(string method, string target, CancellationToken ct);

    /// <summary>
    /// Records what the registry said about this identity when it refused a signed call, so the admin page shows the truth:
    /// a revocation is final, a call from an instance the registry does not treat as verified sends it back to verification.
    /// Any other code changes nothing.
    /// </summary>
    /// <param name="errorCode">The host error code the refusal was mapped to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the identity is saved.</returns>
    Task ReportRefusalAsync(string errorCode, CancellationToken ct);
}
