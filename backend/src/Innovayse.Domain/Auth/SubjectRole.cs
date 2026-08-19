namespace Innovayse.Domain.Auth;

/// <summary>
/// One authorization fact about one subject: this person holds this role here.
///
/// <para>
/// Deliberately not a user record. It carries no email, no name and no password —
/// nothing an identity provider owns — so the same table serves a deployment whose people
/// live in an SSO and one whose people live in <c>AspNetUsers</c>. The subject is whatever
/// the configured provider calls a person: an SSO subject, or a local Identity id.
/// </para>
///
/// <para>
/// Roles stay here rather than moving to the SSO because <see cref="Roles.Reseller"/> and
/// <see cref="Roles.Client"/> are this product's vocabulary — a hosting business's, not an
/// identity provider's — and because someone running this against an SSO they do not own
/// could not add them to it. Keeping them local is also what lets one authorization path
/// serve both modes instead of two.
/// </para>
/// </summary>
public sealed class SubjectRole
{
    /// <summary>The person the role applies to, as the configured identity provider names them.</summary>
    public required string Subject { get; init; }

    /// <summary>One of the names in <see cref="Roles"/>.</summary>
    public required string Role { get; init; }
}
