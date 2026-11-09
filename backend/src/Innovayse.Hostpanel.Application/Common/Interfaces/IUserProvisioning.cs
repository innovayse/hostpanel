using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Creates and edits the records that describe people.
///
/// <para>
/// Separate from <see cref="IIdentityProvider"/>, which only reads, because only one of
/// the two deployments may do this at all. Where this product owns its users it creates
/// and edits them; where an external sign-on provider owns them it must not, and every implementation of this
/// interface in that mode refuses.
/// </para>
///
/// <para>
/// Refusing rather than omitting the interface is deliberate. A flow that still tries to
/// create a person in single sign-on mode is a flow nobody has finished thinking about, and it should
/// say so where it happens, loudly, naming what to do instead, rather than fail to
/// resolve at start-up in a stack trace that names dependency injection.
/// </para>
///
/// <para>
/// Every method answers a <see cref="Result"/> rather than throwing where an external sign-on provider owns the
/// accounts: the refusal is an expected outcome every caller must handle, and a
/// <c>HostpanelErrorCodes.USER_PROVISIONING_NOT_ALLOWED_*</c> code names exactly which flow was
/// refused, so the caller chooses the code without a per-operation exception type to catch.
/// </para>
/// </summary>
public interface IUserProvisioning
{
    /// <summary>
    /// Creates a person and returns their subject.
    /// </summary>
    /// <param name="account">
    /// The address they will sign in with, the password and their name. The password is
    /// required: every flow that reaches this either collects one from an operator or generates
    /// one, and an account created without a password is one nobody can sign in to and nobody
    /// notices until they try. A null given name is derived from the address.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new subject, or a failed <see cref="Result{T}"/> carrying
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<string>> CreateAsync(CreateUserInput account, CancellationToken ct);

    /// <summary>
    /// Updates a person's name and preferred UI language.
    /// </summary>
    /// <param name="profile">Whose profile to update, and the new name and language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or a failed <see cref="Result"/> carrying
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_NAME</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> UpdateProfileAsync(UserProfileUpdate profile, CancellationToken ct);

    /// <summary>
    /// Changes the address a person signs in with, at once, on an operator's word. Every session
    /// the person held ends. A person changing their own address goes through
    /// <see cref="IssueEmailChangeTokenAsync"/> and <see cref="ConfirmEmailChangeAsync"/> instead.
    /// </summary>
    /// <remarks>
    /// Its own method, not a field on <see cref="UpdateProfileAsync"/>. The address is the
    /// credential half of an account rather than a detail on it: changing it changes who
    /// can sign in, it has to stay unique across accounts, and in time it will want a
    /// confirmation step that renaming somebody plainly does not. A caller editing a
    /// display name should not be able to move an account to a different address by
    /// passing the field it happened to read a moment ago.
    /// </remarks>
    /// <param name="subject">Whose sign-in address to change.</param>
    /// <param name="email">The new address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also when no such subject exists here, matching the other writes); a failure carrying
    /// <c>EMAIL_ALREADY_IN_USE</c> when another account holds the new address (checked before
    /// anything is written); or <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> where an external
    /// sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> ChangeEmailAsync(string subject, string email, CancellationToken ct);

    /// <summary>
    /// Applies an operator's edit of an account (name, language and sign-in address) as one
    /// write. The address moves, and every session the person held ends, only when it differs from
    /// the stored one; a save that repeats the stored address leaves the sessions alone.
    /// </summary>
    /// <param name="account">Whose account, and its new name, address and language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success; or a failure carrying <c>USER_NOT_FOUND</c> when the subject names no live account,
    /// <c>EMAIL_ALREADY_IN_USE</c> when another account holds the new address (checked before
    /// anything is written), or <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_NAME</c> where an external
    /// sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> UpdateAccountAsync(UserAccountUpdate account, CancellationToken ct);

    /// <summary>
    /// Checks the current password and issues a token that moves the sign-in to a new address once
    /// it comes back from that address. Nothing about the account changes yet.
    /// </summary>
    /// <param name="start">Whose account, the new address and the current password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The token, to be embedded in a link mailed to the new address; or a failure carrying
    /// <c>IDENTITY_USER_NOT_FOUND</c>, <c>CURRENT_PASSWORD_INVALID</c>, <c>EMAIL_ALREADY_IN_USE</c>, or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<string>> IssueEmailChangeTokenAsync(EmailChangeStart start, CancellationToken ct);

    /// <summary>
    /// Moves the sign-in to the new address when the token issued by
    /// <see cref="IssueEmailChangeTokenAsync"/> matches, marks the address confirmed and ends every
    /// session the person held.
    /// </summary>
    /// <param name="confirmation">Whose account, the new address and the mailed token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying <see langword="true"/> when the address changed,
    /// or <see langword="false"/> when the account is gone or the token is wrong or expired; or a
    /// failure carrying <c>EMAIL_ALREADY_IN_USE</c> when another account took the address meanwhile,
    /// or <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<bool>> ConfirmEmailChangeAsync(EmailChangeConfirmation confirmation, CancellationToken ct);

    /// <summary>
    /// Deletes an account. Records that reference the subject are left alone.
    /// </summary>
    /// <param name="subject">Whose account to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or a failed <see cref="Result"/> carrying
    /// <c>USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> DeleteAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Removes an account for real, so its address is free to register again. Only for compensation:
    /// an account this same request created moments ago and that nothing has used yet (no client
    /// row, no session, no record naming it) because the work that was to follow it failed.
    /// </summary>
    /// <remarks>
    /// <see cref="DeleteAsync"/> only marks an account deleted and keeps its address taken, which is
    /// right for a person who really existed. An account nobody ever held would leave an address
    /// that nobody can use again, so this one is deleted outright. Never call it for an account
    /// that existed before the request, or that anything may already reference.
    /// </remarks>
    /// <param name="subject">The account this request created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also when no such subject exists here), or a failed <see cref="Result"/> carrying
    /// <c>USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT</c> where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> PurgeUnusedAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Sets a password directly, without the holder's involvement.
    /// </summary>
    /// <param name="subject">Whose password to set.</param>
    /// <param name="password">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying <see langword="true"/> when the subject was
    /// found and the password set, or <see langword="false"/> when no such subject exists here:
    /// returned rather than a failure, because "not found" is an answer the caller
    /// (<c>SetUserPasswordHandler</c>) turns into its own <c>Result</c> code. A failed
    /// <see cref="Result{T}"/> carries <c>USER_PROVISIONING_NOT_ALLOWED_SET_PASSWORD</c> where an
    /// the sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<bool>> SetPasswordAsync(string subject, string password, CancellationToken ct);

    /// <summary>
    /// Issues a token the holder can use to choose a new password.
    /// </summary>
    /// <param name="subject">Whose reset token to issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying the token, to be embedded in a reset link, or
    /// <see langword="null"/> when no such subject exists here: returned rather than a failure,
    /// for the same reason as <see cref="SetPasswordAsync"/>'s <see langword="bool"/>. A failed
    /// <see cref="Result{T}"/> carries <c>USER_PROVISIONING_NOT_ALLOWED_ISSUE_PASSWORD_RESET</c>
    /// where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<string?>> IssuePasswordResetTokenAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Sets a new password using a token issued by <see cref="IssuePasswordResetTokenAsync"/>.
    /// </summary>
    /// <param name="reset">The account's email address, the reset token and the new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying <see langword="true"/> when the password was
    /// changed, or <see langword="false"/> when no such account exists or the token is invalid or
    /// expired. A failed <see cref="Result{T}"/> carries <c>USER_PROVISIONING_NOT_ALLOWED_SET_PASSWORD</c>
    /// where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<bool>> ResetPasswordWithTokenAsync(PasswordResetWithToken reset, CancellationToken ct);

    /// <summary>
    /// Marks an address confirmed using a token issued by <see cref="IssueEmailConfirmationTokenAsync"/>.
    /// </summary>
    /// <param name="confirmation">The account's email address and the mailed confirmation token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying <see langword="true"/> when the address was
    /// confirmed, or <see langword="false"/> when no such account exists or the token is invalid or
    /// expired. A failed <see cref="Result{T}"/> carries <c>USER_PROVISIONING_NOT_ALLOWED_CONFIRM_EMAIL</c>
    /// where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<bool>> ConfirmEmailWithTokenAsync(EmailConfirmationWithToken confirmation, CancellationToken ct);

    /// <summary>
    /// Marks a person's email address as confirmed without a mailed token.
    /// </summary>
    /// <remarks>
    /// For flows that already prove the holder controls the address or the installation, such as
    /// consuming the one-time setup token. Ordinary registration must keep using the token flow.
    /// </remarks>
    /// <param name="subject">Whose address to confirm.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also when no such subject exists here, matching the other writes), or a failed
    /// <see cref="Result"/> carrying <c>USER_PROVISIONING_NOT_ALLOWED_CONFIRM_EMAIL</c> where an
    /// the sign-on provider owns the accounts.
    /// </returns>
    public Task<Result> ConfirmEmailAsync(string subject, CancellationToken ct);

    /// <summary>
    /// Issues a token that confirms a person's email address when it comes back through
    /// <c>POST /api/auth/confirm-email</c>.
    /// </summary>
    /// <param name="subject">Whose address the token confirms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> carrying the token, to be embedded in a verification
    /// link, or <see langword="null"/> when no such subject exists here: returned rather than a
    /// failure, for the same reason as <see cref="IssuePasswordResetTokenAsync"/>. A failed
    /// <see cref="Result{T}"/> carries <c>USER_PROVISIONING_NOT_ALLOWED_ISSUE_EMAIL_CONFIRMATION</c>
    /// where an external sign-on provider owns the accounts.
    /// </returns>
    public Task<Result<string?>> IssueEmailConfirmationTokenAsync(string subject, CancellationToken ct);
}
