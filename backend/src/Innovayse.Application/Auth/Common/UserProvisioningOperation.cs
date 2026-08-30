namespace Innovayse.Application.Auth.Common;

/// <summary>
/// Which account-writing flow was attempted, for the refusal that answers it where an SSO owns
/// the accounts.
/// </summary>
/// <remarks>
/// <para>
/// One member per method on <c>IUserProvisioning</c>. It exists so the refusal can name the flow
/// in the caller's own language: the sentence used to be assembled from an English fragment
/// ("change someone's sign-in address") spliced into English prose, which is untranslatable --
/// a Russian sentence cannot take an English infinitive phrase, and the six phrases decline
/// differently in Russian and Armenian besides. A member here maps to a resource key of its own
/// rather than to a <c>{0}</c> placeholder, so each of the six is written once per language by
/// somebody who can see the whole sentence.
/// </para>
/// <para>
/// It is not a wire contract. The caller branches on
/// <see cref="UserProvisioningNotAllowedException.Code"/>, which is one code for all six: what a
/// caller can do about any of them is the same thing, and splitting the code six ways would give
/// the frontend six branches that all render the sentence the API already sent.
/// </para>
/// </remarks>
public enum UserProvisioningOperation
{
    /// <summary>Creating an account. <c>IUserProvisioning.CreateAsync</c>.</summary>
    CreateAccount,

    /// <summary>Changing a person's name. <c>IUserProvisioning.UpdateProfileAsync</c>.</summary>
    ChangeName,

    /// <summary>Changing the address a person signs in with. <c>IUserProvisioning.ChangeEmailAsync</c>.</summary>
    ChangeEmail,

    /// <summary>Deleting an account. <c>IUserProvisioning.DeleteAsync</c>.</summary>
    DeleteAccount,

    /// <summary>Setting a password directly. <c>IUserProvisioning.SetPasswordAsync</c>.</summary>
    SetPassword,

    /// <summary>Issuing a password-reset token. <c>IUserProvisioning.IssuePasswordResetTokenAsync</c>.</summary>
    IssuePasswordReset,
}
