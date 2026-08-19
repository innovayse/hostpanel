namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Creates and edits people in this product's own <c>AspNetUsers</c> table.
///
/// <para>
/// What a deployment with no SSO gets, and unchanged behaviour: the same Identity calls
/// this product has always made, behind a name the SSO-owned deployment can refuse.
/// </para>
/// </summary>
public sealed class LocalUserProvisioning(UserManager<AppUser> users) : IUserProvisioning
{
    /// <inheritdoc/>
    public async Task<string> CreateAsync(
        string email, string password, string? firstName, string? lastName, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            // Falling back to the local part of the address rather than to an empty
            // string: a person with no name at all shows as a blank row everywhere.
            FirstName = string.IsNullOrWhiteSpace(firstName) ? email.Split('@')[0] : firstName,
            LastName = lastName ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }

    /// <inheritdoc/>
    public async Task UpdateProfileAsync(
        string subject, string firstName, string lastName, string? language, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject);
        if (user is null) return;

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Language = language;

        await ThrowIfFailedAsync(users.UpdateAsync(user), subject);
    }

    /// <inheritdoc/>
    public async Task ChangeEmailAsync(string subject, string email, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject);
        if (user is null) return;

        // The user name is the address here — Identity was configured that way, and
        // leaving it behind would let somebody sign in under the old address forever.
        user.Email = email;
        user.UserName = email;

        await ThrowIfFailedAsync(users.UpdateAsync(user), subject);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string subject, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject);
        if (user is null || user.IsDeleted) return;

        // Marked, not removed. See the note on AppUser.DeletedAt: the client and invoice
        // rows that reference this id outlive the account, and a removed row turns every
        // one of them into a blank nobody can identify.
        //
        // The address stays on the row, so it stays taken — which is what the SSO does
        // with its own deleted accounts, and it keeps the two stores answering alike. It
        // also means signing the same person back up needs the old account restored rather
        // than silently shadowed by a second one holding the same address.
        user.DeletedAt = DateTimeOffset.UtcNow;

        // Locked out at the same time, so any path that reads the row while ignoring the
        // deleted marker still refuses the sign-in rather than letting it through.
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await ThrowIfFailedAsync(users.UpdateAsync(user), subject);
    }

    /// <inheritdoc/>
    public async Task SetPasswordAsync(string subject, string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject)
            ?? throw new InvalidOperationException($"User {subject} not found.");

        // Through a reset token rather than by removing and re-adding the password. The
        // remove-then-add pair leaves the account with no password at all if the second
        // call fails, and this one is atomic.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        await ThrowIfFailedAsync(users.ResetPasswordAsync(user, token, password), subject);
    }

    /// <inheritdoc/>
    public async Task<string> IssuePasswordResetTokenAsync(string subject, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject)
            ?? throw new InvalidOperationException($"User {subject} not found.");

        return await users.GeneratePasswordResetTokenAsync(user);
    }

    /// <summary>
    /// Identity reports a rejected write by returning it, not by throwing. A duplicate
    /// address or a failed validation would otherwise be indistinguishable from success —
    /// the caller commits its own changes and the account keeps the details it had.
    /// </summary>
    private static async Task ThrowIfFailedAsync(Task<IdentityResult> update, string subject)
    {
        var result = await update;
        if (result.Succeeded) return;

        throw new InvalidOperationException(
            $"Could not update {subject}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
