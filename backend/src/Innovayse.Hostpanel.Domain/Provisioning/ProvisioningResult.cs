using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>
/// Result of a provisioning operation returned by the <c>IProvisioningProvider</c> port
/// (<c>Innovayse.Hostpanel.Application.Common.Interfaces</c>: Domain cannot reference it, so
/// this cannot be a resolvable <c>cref</c>). Built through <see cref="Created"/>,
/// <see cref="AlreadyExists"/>, <see cref="Pending"/> and <see cref="Refused"/>.
/// </summary>
/// <param name="Outcome">How the provider answered.</param>
/// <param name="ProvisioningRef">
/// Provider-assigned reference of the created or existing account; null when refused.
/// </param>
/// <param name="ExistingAccountDomain">
/// Primary domain of the account that already exists, when <paramref name="Outcome"/> is
/// <see cref="ProvisioningOutcome.AlreadyExists"/> and the provider can tell; null otherwise.
/// </param>
/// <param name="ErrorMessage">Human-readable error description; null unless refused.</param>
public sealed record ProvisioningResult(
    ProvisioningOutcome Outcome,
    string? ProvisioningRef,
    string? ExistingAccountDomain,
    string? ErrorMessage)
{
    /// <summary>Builds the answer for an account the provider created.</summary>
    /// <param name="provisioningRef">Provider-assigned reference of the new account.</param>
    /// <returns>A <see cref="ProvisioningOutcome.Created"/> result.</returns>
    public static ProvisioningResult Created(string provisioningRef) =>
        new(ProvisioningOutcome.Created, provisioningRef, null, null);

    /// <summary>Builds the answer for a username the panel already holds an account under.</summary>
    /// <param name="provisioningRef">Provider-assigned reference of the existing account.</param>
    /// <param name="existingAccountDomain">Primary domain of the existing account, or null when the provider cannot tell.</param>
    /// <returns>An <see cref="ProvisioningOutcome.AlreadyExists"/> result.</returns>
    public static ProvisioningResult AlreadyExists(string provisioningRef, string? existingAccountDomain) =>
        new(ProvisioningOutcome.AlreadyExists, provisioningRef, existingAccountDomain, null);

    /// <summary>Builds the answer for an account the panel accepted and is still creating.</summary>
    /// <param name="provisioningRef">Provider-assigned reference of the account being created.</param>
    /// <returns>A <see cref="ProvisioningOutcome.Pending"/> result.</returns>
    public static ProvisioningResult Pending(string provisioningRef) =>
        new(ProvisioningOutcome.Pending, provisioningRef, null, null);

    /// <summary>Builds the answer for a request the panel refused.</summary>
    /// <param name="errorMessage">Why, for the log only; never put on the wire.</param>
    /// <returns>A <see cref="ProvisioningOutcome.Refused"/> result.</returns>
    public static ProvisioningResult Refused(string errorMessage) =>
        new(ProvisioningOutcome.Refused, null, null, errorMessage);
}
