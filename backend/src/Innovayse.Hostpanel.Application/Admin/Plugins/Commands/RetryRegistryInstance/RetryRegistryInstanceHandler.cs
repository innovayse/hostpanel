using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RetryRegistryInstance;

/// <summary>
/// Handles <see cref="RetryRegistryInstanceCommand"/>: forces one registration run and answers with where it left the
/// identity. A run that fails is an answer, not a refusal: the state says what went wrong, in the caller's language.
/// </summary>
public sealed class RetryRegistryInstanceHandler
{
    /// <summary>Registers and verifies the installation.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>Initializes the handler.</summary>
    /// <param name="enrollment">Registers and verifies the installation.</param>
    public RetryRegistryInstanceHandler(RegistryInstanceEnrollment enrollment)
    {
        _enrollment = enrollment;
    }

    /// <summary>Runs the registration and returns the state.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state after the run; <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off.</returns>
    public async Task<Result<RegistryInstanceDto>> HandleAsync(RetryRegistryInstanceCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var outcome = await _enrollment.EnsureVerifiedAsync(force: true, ct);
        if (outcome.ErrorCode == HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED)
        {
            return Result<RegistryInstanceDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        }

        return Result<RegistryInstanceDto>.Success(await _enrollment.GetStatusAsync(ct));
    }
}
