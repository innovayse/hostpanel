namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Creates and edits the records that describe people.
///
/// <para>
/// Separate from <see cref="IIdentityProvider"/>, which only reads, because only one of
/// the two deployments may do this at all. Where this product owns its users it creates
/// and edits them; where an SSO owns them it must not, and every implementation of this
/// interface in that mode refuses.
/// </para>
///
/// <para>
/// Refusing rather than omitting the interface is deliberate. A flow that still tries to
/// create a person in SSO mode is a flow nobody has finished thinking about, and it should
/// say so where it happens — loudly, naming what to do instead — rather than fail to
/// resolve at start-up in a stack trace that names dependency injection.
/// </para>
/// </summary>
public interface IUserProvisioning
{
    /// <summary>
    /// Creates a person and returns their subject.
    /// </summary>
    /// <param name="email">The address they will sign in with.</param>
    /// <param name="password">
    /// The password to set. Required: every flow that reaches this either collects one
    /// from an operator or generates one, and an account created without a password is one
    /// nobody can sign in to and nobody notices until they try.
    /// </param>
    /// <param name="firstName">Given name, or null to derive one from the address.</param>
    /// <param name="lastName">Family name, or null for none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task<string> CreateAsync(
        string email, string password, string? firstName, string? lastName, CancellationToken ct);

    /// <summary>
    /// Updates a person's name and preferred UI language.
    /// </summary>
    /// <param name="subject">Whose profile to update.</param>
    /// <param name="firstName">The new given name.</param>
    /// <param name="lastName">The new family name.</param>
    /// <param name="language">Preferred UI language, or null to leave it unset.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task UpdateProfileAsync(
        string subject, string firstName, string lastName, string? language, CancellationToken ct);

    /// <summary>
    /// Changes the address a person signs in with.
    /// </summary>
    /// <remarks>
    /// Its own method, not a field on <see cref="UpdateProfileAsync"/>. The address is the
    /// credential half of an account rather than a detail on it: changing it changes who
    /// can sign in, it has to stay unique across accounts, and in time it will want a
    /// confirmation step that renaming somebody plainly does not. A caller editing a
    /// display name should not be able to move an account to a different address by
    /// passing the field it happened to read a moment ago.
    /// </remarks>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task ChangeEmailAsync(string subject, string email, CancellationToken ct);

    /// <summary>
    /// Deletes an account. Records that reference the subject are left alone.
    /// </summary>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task DeleteAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Sets a password directly, without the holder's involvement.
    /// </summary>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task SetPasswordAsync(string subject, string password, CancellationToken ct);

    /// <summary>
    /// Issues a token the holder can use to choose a new password.
    /// </summary>
    /// <returns>The token, to be embedded in a reset link.</returns>
    /// <exception cref="UserProvisioningNotAllowedException">
    /// Where an SSO owns the accounts.
    /// </exception>
    Task<string> IssuePasswordResetTokenAsync(string subject, CancellationToken ct);
}

/// <summary>
/// Thrown when a flow tries to create or edit a person in a deployment whose people belong
/// to an SSO.
/// </summary>
public sealed class UserProvisioningNotAllowedException(string operation)
    : InvalidOperationException(
        $"This deployment's accounts belong to the SSO, so hostpanel cannot {operation}. " +
        "The person must exist in the SSO first, and changes to their name or address are " +
        "made there.");
