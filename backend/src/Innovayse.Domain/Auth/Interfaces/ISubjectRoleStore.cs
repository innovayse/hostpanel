namespace Innovayse.Domain.Auth.Interfaces;

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
    Task<IReadOnlyList<string>> GetRolesAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Grants a role. Granting one already held is not an error — the production
    /// migration replays grants, and a re-run has to be harmless.
    /// </summary>
    Task AddAsync(string subject, string role, CancellationToken ct);

    /// <summary>Revokes a role. Revoking one not held is not an error.</summary>
    Task RemoveAsync(string subject, string role, CancellationToken ct);
}
