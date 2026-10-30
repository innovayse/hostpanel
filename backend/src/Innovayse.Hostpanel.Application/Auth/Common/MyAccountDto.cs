namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>The caller's email, local roles and client account permissions, for clients that cannot decode them from the token.</summary>
/// <param name="Email">The account's email address.</param>
/// <param name="Roles">The roles granted locally to the account.</param>
/// <param name="EmailVerified">Whether the credential says the email is confirmed; <c>null</c> when it carries no claim.</param>
/// <param name="ClientPermissions">
/// The <c>ClientPermission</c> flags the caller holds on a client account as an integer: 8191 (all) for the owner of
/// a client record, the stored grant for a linked user, 0 for a caller with no account. It is what the portal reads to
/// hide what the user cannot use; the server enforces the same value on every portal action.
/// </param>
/// <param name="ClientId">
/// The client account the caller acts for: the record they own, the one they are linked to, or the one an identity
/// plugin links their account to. <c>null</c> when they act for none, including a plugin member whose account has no
/// client yet and a caller linked to several accounts.
/// </param>
/// <param name="ExternalAccount">
/// The account in another system the client is linked to; for a plugin member whose account has no client yet, the
/// account the plugin named. <c>null</c> when the caller acts for no client or the client is not linked to one.
/// </param>
public sealed record MyAccountDto(
    string Email,
    IReadOnlyList<string> Roles,
    bool? EmailVerified,
    int ClientPermissions,
    int? ClientId,
    ExternalAccountDto? ExternalAccount);
