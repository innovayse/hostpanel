using Innovayse.Hostpanel.Application.Auth.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Abstraction over ASP.NET Core Identity user management operations.
/// Implemented in Infrastructure; isolates Application from <c>IdentityUser</c> concrete types.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Creates a new user with the specified email and password.
    /// </summary>
    /// <param name="input">The new account's email, password and optional name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created user's ID on success.
    /// Throws <see cref="InvalidOperationException"/> on failure.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when user creation fails with Identity errors.</exception>
    public Task<string> CreateAsync(CreateUserInput input, CancellationToken ct);

    /// <summary>
    /// Assigns a role to an existing user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="role">The role name to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the role is assigned.</returns>
    public Task AddToRoleAsync(string userId, string role, CancellationToken ct);

    /// <summary>
    /// Checks a password sign-in. A wrong password counts toward the account lockout, a right one
    /// clears the count, and an unknown address costs the same password hash check as a known one so
    /// the answer time does not tell whether an account exists.
    /// </summary>
    /// <param name="input">The email and the plain-text password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome, with the account when it succeeded.</returns>
    public Task<PasswordSignInResult> SignInWithPasswordAsync(PasswordSignInInput input, CancellationToken ct);

    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, or <see langword="null"/> if not found.</returns>
    public Task<UserIdentity?> FindByIdAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Checks whether the specified user's email has been confirmed.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user's email is confirmed; false otherwise.</returns>
    public Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Updates the last login timestamp for a user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the timestamp is saved.</returns>
    public Task UpdateLastLoginAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Returns whether two-factor authentication is currently enabled for the user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if 2FA is enabled; false otherwise.</returns>
    public Task<bool> IsTwoFactorEnabledAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Generates a new Base32-encoded TOTP secret and stores it as the account's pending secret.
    /// Does not enable 2FA and never replaces the secret an enabled account signs in with;
    /// <see cref="EnableTwoFactorAsync"/> promotes it once a code proves the app holds it.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly generated Base32-encoded TOTP secret.</returns>
    public Task<string> GenerateTwoFactorSecretAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Checks <paramref name="code"/> against the account's pending secret and, when it matches,
    /// makes that secret the active one and marks two-factor authentication as enabled.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="code">The 6-digit TOTP code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when it was enabled; false when there is no pending secret or the code did not match.</returns>
    public Task<bool> EnableTwoFactorAsync(string userId, string code, CancellationToken ct);

    /// <summary>
    /// Disables two-factor authentication for the user, clears the active and pending TOTP
    /// secrets and changes the security stamp, so every session issued before ends.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the flag and the secret are cleared.</returns>
    public Task DisableTwoFactorAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Verifies a 6-digit TOTP code against the user's stored secret.
    /// Accepts codes within the RFC-specified network delay window.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="code">The 6-digit TOTP code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the code is valid within the allowed time window; false otherwise.</returns>
    public Task<bool> VerifyTwoFactorCodeAsync(string userId, string code, CancellationToken ct);

    /// <summary>
    /// Checks the account's current password.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the account exists and the password is its current one; false otherwise.</returns>
    public Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken ct);

    /// <summary>
    /// Returns the account's current session version: a value derived from its security stamp that
    /// changes whenever the password is reset, two-factor is switched off or the sign-in address
    /// changes. An access token carries the version it was issued with and stops being accepted
    /// once the account's version moves on.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current version, or <see langword="null"/> when the account does not exist or is deleted.</returns>
    public Task<string?> GetSessionVersionAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Retrieves all roles assigned to the specified user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The role names assigned to the user.</returns>
    public Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct);
}
