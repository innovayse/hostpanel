namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// What a deployment whose people belong to an SSO gets: a provisioner that refuses.
///
/// <para>
/// It exists so the refusal happens at the call, naming the flow that attempted it, rather
/// than as a missing registration at start-up. Nothing here writes, and nothing here can
/// be made to write — which is the guarantee the whole arrangement rests on.
/// </para>
/// </summary>
public sealed class SsoModeUserProvisioning : IUserProvisioning
{
    /// <inheritdoc/>
    public Task<string> CreateAsync(
        string email, string? firstName, string? lastName, CancellationToken ct) =>
        throw new UserProvisioningNotAllowedException("create an account");

    /// <inheritdoc/>
    public Task UpdateNameAsync(
        string subject, string firstName, string lastName, CancellationToken ct) =>
        throw new UserProvisioningNotAllowedException("change someone's name");
}
