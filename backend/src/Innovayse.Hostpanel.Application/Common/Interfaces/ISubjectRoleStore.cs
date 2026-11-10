using Innovayse.Hostpanel.Domain.Auth.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads and writes the roles held by a subject.
///
/// <para>
/// The one way authorization asks what someone may do, in either mode. Nothing here takes
/// a user entity, only the subject string, which is what keeps the caller from needing to
/// know where people are stored.
/// </para>
/// </summary>
public interface ISubjectRoleStore
{
    /// <summary>The roles this subject holds, empty if none.</summary>
    /// <param name="subject">The subject, as the configured identity provider names them.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The held roles.</returns>
    public Task<IReadOnlyList<Role>> GetRolesAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Grants a role. Granting one already held is not an error, because a migration pull
    /// replays grants, and a re-run has to be harmless.
    /// </summary>
    /// <param name="subject">The subject, as the configured identity provider names them.</param>
    /// <param name="role">The role asked about or changed.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes once the grant is saved.</returns>
    public Task AddAsync(string subject, Role role, CancellationToken ct);

    /// <summary>Revokes a role. Revoking one not held is not an error.</summary>
    /// <param name="subject">The subject, as the configured identity provider names them.</param>
    /// <param name="role">The role asked about or changed.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes once the grant is gone.</returns>
    public Task RemoveAsync(string subject, Role role, CancellationToken ct);

    /// <summary>
    /// Whether any subject currently holds this role.
    ///
    /// <para>
    /// Backs the first-run bootstrap check: a fresh deployment offers the first person to
    /// sign in a chance to become Admin, and stops offering it the moment anyone holds
    /// that role. The question is keyed by subject rather than by local user rows because a
    /// deployment whose people live in an external sign-on provider has no local user rows.
    /// </para>
    /// </summary>
    /// <param name="role">The role asked about or changed.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>Whether at least one subject holds the role.</returns>
    public Task<bool> AnyHasRoleAsync(Role role, CancellationToken ct);

    /// <summary>
    /// The subjects that hold a role. Backs the guard that keeps an installation from losing its
    /// last administrator; the caller decides which of them are still live accounts.
    /// </summary>
    /// <param name="role">The role asked about or changed.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The holders' subjects, each once.</returns>
    public Task<IReadOnlyList<string>> GetHoldersAsync(Role role, CancellationToken ct);

    /// <summary>
    /// Whether one subject holds a role, counting every store the deployment authorizes on, the
    /// same union <see cref="GetHoldersAsync"/> answers for the whole role.
    /// </summary>
    /// <param name="subject">The subject, as the configured identity provider names them.</param>
    /// <param name="role">The role asked about or changed.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>Whether the subject holds the role.</returns>
    public Task<bool> HoldsAsync(string subject, Role role, CancellationToken ct);
}
