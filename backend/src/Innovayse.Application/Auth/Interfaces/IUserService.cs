namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Abstraction over ASP.NET Core Identity user management operations.
/// Implemented in Infrastructure; isolates Application from <c>IdentityUser</c> concrete types.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Creates a new user with the specified email and password.
    /// </summary>
    /// <param name="email">The user's email address (also used as username).</param>
    /// <param name="password">The plain-text password to hash and store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="firstName">Optional first name to set on the user profile.</param>
    /// <param name="lastName">Optional last name to set on the user profile.</param>
    /// <returns>
    /// The newly created user's ID on success.
    /// Throws <see cref="InvalidOperationException"/> on failure.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when user creation fails with Identity errors.</exception>
    Task<string> CreateAsync(string email, string password, CancellationToken ct, string? firstName = null, string? lastName = null);

    /// <summary>
    /// Assigns a role to an existing user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="role">The role name to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddToRoleAsync(string userId, string role, CancellationToken ct);

    /// <summary>
    /// Finds a user by email and verifies the supplied password.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Tuple of user ID and email if credentials are valid; <see langword="null"/> otherwise.
    /// </returns>
    Task<(string Id, string Email)?> FindByEmailAndPasswordAsync(string email, string password, CancellationToken ct);

    /// <summary>
    /// Finds a user by their exact email address (case-insensitive).
    /// </summary>
    /// <param name="email">The exact email address to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of user ID and email, or <see langword="null"/> if not found.</returns>
    Task<(string Id, string Email)?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of user ID and email, or <see langword="null"/> if not found.</returns>
    Task<(string Id, string Email)?> FindByIdAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Retrieves the primary role for a user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first role name, or <see langword="null"/> if the user has no roles.</returns>
    Task<string?> GetPrimaryRoleAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Retrieves email addresses for a batch of user IDs.
    /// </summary>
    /// <param name="userIds">The user identifiers to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping user ID to email address.</returns>
    Task<Dictionary<string, string>> GetEmailsByIdsAsync(IEnumerable<string> userIds, CancellationToken ct);

    /// <summary>
    /// Finds user IDs whose email contains the given search term (case-insensitive).
    /// </summary>
    /// <param name="emailSearch">Partial email to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching user IDs.</returns>
    Task<List<string>> FindUserIdsByEmailAsync(string emailSearch, CancellationToken ct);

    /// <summary>
    /// Checks whether any users exist in the system.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if at least one user exists; false otherwise.</returns>
    Task<bool> AnyUsersExistAsync(CancellationToken ct);

    /// <summary>
    /// Generates an email confirmation token for the specified user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated email confirmation token string.</returns>
    Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Confirms the user's email address using the provided token.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="token">The email confirmation token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if confirmation succeeded; false otherwise.</returns>
    Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct);

    /// <summary>
    /// Checks whether the specified user's email has been confirmed.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user's email is confirmed; false otherwise.</returns>
    Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of all users, optionally filtered by search term.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="search">Optional search term (matches name or email).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of user list items and total count.</returns>
    Task<(List<Admin.DTOs.UserListItemDto> Items, int TotalCount)> ListUsersAsync(
        int page, int pageSize, string? search, CancellationToken ct);

    /// <summary>
    /// Returns a user by ID with their linked client accounts.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO or null if not found.</returns>
    Task<Admin.DTOs.UserDetailDto?> GetUserWithAccountsAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Updates a user's profile fields (name, email, language).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="firstName">New first name.</param>
    /// <param name="lastName">New last name.</param>
    /// <param name="email">New email address.</param>
    /// <param name="language">Preferred language code or null for default.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateUserAsync(string userId, string firstName, string lastName, string email, string? language, CancellationToken ct);

    /// <summary>
    /// Deletes an Identity user. Does not remove linked client records.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteUserAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Generates a password reset token for the given user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The password reset token string.</returns>
    Task<string> GeneratePasswordResetTokenAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Sets a new password for a user (admin action, no old password required).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ChangePasswordAsync(string userId, string newPassword, CancellationToken ct);

    /// <summary>
    /// Resets a user's password using a previously issued reset token.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="token">The password reset token.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the reset succeeded; false otherwise.</returns>
    Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken ct);

    /// <summary>
    /// Updates the last login timestamp for a user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateLastLoginAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Returns whether two-factor authentication is currently enabled for the user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if 2FA is enabled; false otherwise.</returns>
    Task<bool> IsTwoFactorEnabledAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Generates and persists a new Base32-encoded TOTP secret for the user.
    /// Does not enable 2FA — the caller must confirm with a valid code first.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly generated Base32-encoded TOTP secret.</returns>
    Task<string> GenerateTwoFactorSecretAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Retrieves the stored Base32-encoded TOTP secret for the user, or null if not set.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The TOTP secret, or null if 2FA has not been configured.</returns>
    Task<string?> GetTwoFactorSecretAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Marks two-factor authentication as enabled for the user in Identity.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnableTwoFactorAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Disables two-factor authentication for the user and clears the stored TOTP secret.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DisableTwoFactorAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Verifies a 6-digit TOTP code against the user's stored secret.
    /// Accepts codes within the RFC-specified network delay window.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="code">The 6-digit TOTP code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the code is valid within the allowed time window; false otherwise.</returns>
    Task<bool> VerifyTwoFactorCodeAsync(string userId, string code, CancellationToken ct);

    /// <summary>Finds a user by their SSO subject identifier (the 'sub' claim).</summary>
    Task<(string Id, string Email)?> FindBySsoSubjectAsync(string sub, CancellationToken ct);

    /// <summary>
    /// Provisions a local AppUser record for an SSO-authenticated user.
    /// If a user with this email already exists, links the SsoSubjectId.
    /// If not, creates a new AppUser with the Client role assigned.
    /// </summary>
    Task ProvisionSsoUserAsync(string sub, string email, string firstName, string lastName, CancellationToken ct);

    /// <summary>
    /// Retrieves all roles assigned to the specified user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of role names assigned to the user.</returns>
    Task<IList<string>> GetRolesAsync(string userId, CancellationToken ct);
}
