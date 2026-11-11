using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Identity;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// Creates and edits people in this product's own <c>AspNetUsers</c> table.
///
/// <para>
/// What a deployment with no external sign-on provider gets, and unchanged behaviour: the same Identity calls
/// this product has always made, behind a name the provider-owned deployment can refuse.
/// </para>
/// </summary>
public sealed class LocalUserProvisioning : IUserProvisioning
{
    /// <summary>Identity's user store, the only place local accounts are written.</summary>
    private readonly UserManager<AppUser> _users;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="LocalUserProvisioning"/> class.</summary>
    /// <param name="users">Identity's user store, the only place local accounts are written.</param>
    /// <param name="clock">The source of the current moment.</param>
    public LocalUserProvisioning(UserManager<AppUser> users, IClock clock)
    {
        _users = users;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> CreateAsync(CreateUserInput account, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        var (email, password, firstName, lastName) = account;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            // Falling back to the local part of the address rather than to an empty
            // string: a person with no name at all shows as a blank row everywhere.
            FirstName = string.IsNullOrWhiteSpace(firstName) ? email.Split('@')[0] : firstName,
            LastName = lastName ?? string.Empty,
        };

        var result = await _users.CreateAsync(user, password);

        // A taken address is the caller's to fix (sign in instead), not a server fault, so it is
        // answered with its own code; the user name is the email, so either duplicate means it.
        var describer = _users.ErrorDescriber;
        if (result.Errors.Any(e =>
            e.Code == describer.DuplicateEmail(email).Code || e.Code == describer.DuplicateUserName(email).Code))
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.ACCOUNT_EMAIL_EXISTS);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return Result<string>.Success(user.Id);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateProfileAsync(UserProfileUpdate profile, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var (subject, firstName, lastName, language) = profile;
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result.Success();
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Language = language;

        await ThrowIfFailedAsync(_users.UpdateAsync(user), subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ChangeEmailAsync(string subject, string email, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result.Success();
        }

        // An address another account holds is the caller's to fix, answered as a refusal before
        // anything is written; left to Identity's uniqueness check it would surface as a 500.
        var holder = await _users.FindByEmailAsync(email);
        if (holder is not null && holder.Id != user.Id)
        {
            return Result.FailureCode(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
        }

        // The user name is the address here: Identity was configured that way, and
        // leaving it behind would let somebody sign in under the old address forever.
        user.Email = email;
        user.UserName = email;

        // Saved through the stamp update, so the new address and the end of every session the
        // person held are one write.
        await ThrowIfFailedAsync(_users.UpdateSecurityStampAsync(user), subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAccountAsync(UserAccountUpdate account, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);

        var user = await _users.FindByIdAsync(account.Subject);
        if (user is null || user.IsDeleted)
        {
            return Result.FailureCode(HostpanelErrorCodes.USER_NOT_FOUND);
        }

        // Uniqueness first, so a taken address refuses the whole edit before anything changes.
        var emailChanged = !string.Equals(user.Email, account.Email, StringComparison.Ordinal);
        if (emailChanged)
        {
            var holder = await _users.FindByEmailAsync(account.Email);
            if (holder is not null && holder.Id != user.Id)
            {
                return Result.FailureCode(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
            }

            // The user name is the address here, as everywhere this class writes one.
            user.Email = account.Email;
            user.UserName = account.Email;
        }

        user.FirstName = account.FirstName;
        user.LastName = account.LastName;
        user.Language = account.Language;

        // One write either way. A moved address goes through the stamp update, which saves every
        // field above and ends the person's sessions; an unchanged one keeps the stamp, so editing
        // a name does not sign the person out everywhere.
        var saved = emailChanged ? _users.UpdateSecurityStampAsync(user) : _users.UpdateAsync(user);
        await ThrowIfFailedAsync(saved, account.Subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<string>> IssueEmailChangeTokenAsync(EmailChangeStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(start);

        var user = await _users.FindByIdAsync(start.Subject);
        if (user is null || user.IsDeleted)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        }

        if (!await _users.CheckPasswordAsync(user, start.CurrentPassword))
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.CURRENT_PASSWORD_INVALID);
        }

        // Any holder at all, this account included: moving to the address it already has is not a change.
        var holder = await _users.FindByEmailAsync(start.NewEmail);
        if (holder is not null)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
        }

        var token = await _users.GenerateChangeEmailTokenAsync(user, start.NewEmail);
        return Result<string>.Success(token);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ConfirmEmailChangeAsync(EmailChangeConfirmation confirmation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        var user = await _users.FindByIdAsync(confirmation.Subject);
        if (user is null || user.IsDeleted)
        {
            return Result<bool>.Success(false);
        }

        // The token is bound to this account, this new address and the account's current stamp,
        // so a token for another address, or one issued before a password reset, does not match.
        var valid = await _users.VerifyUserTokenAsync(
            user,
            _users.Options.Tokens.ChangeEmailTokenProvider,
            UserManager<AppUser>.GetChangeEmailTokenPurpose(confirmation.NewEmail),
            confirmation.Token);
        if (!valid)
        {
            return Result<bool>.Success(false);
        }

        var holder = await _users.FindByEmailAsync(confirmation.NewEmail);
        if (holder is not null)
        {
            return Result<bool>.FailureCode(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
        }

        // The address the link reached is now proven, and the user name follows it as on every
        // other address change. Saved through the stamp update, so the change and the end of
        // every earlier session are one write.
        user.Email = confirmation.NewEmail;
        user.UserName = confirmation.NewEmail;
        user.EmailConfirmed = true;
        await ThrowIfFailedAsync(_users.UpdateSecurityStampAsync(user), confirmation.Subject);
        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result> PurgeUnusedAsync(string subject, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result.Success();
        }

        // Removed outright, unlike DeleteAsync: nothing references an account that was created and
        // abandoned inside one request, and a marked row would keep its address taken forever.
        await ThrowIfFailedAsync(_users.DeleteAsync(user), subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAsync(string subject, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null || user.IsDeleted)
        {
            return Result.Success();
        }

        // Marked, not removed. See the note on AppUser.DeletedAt: the client and invoice
        // rows that reference this id outlive the account, and a removed row turns every
        // one of them into a blank nobody can identify.
        //
        // The address stays on the row, so it stays taken, which is what the sign-on provider does
        // with its own deleted accounts, and it keeps the two stores answering alike. It
        // also means signing the same person back up needs the old account restored rather
        // than silently shadowed by a second one holding the same address.
        user.DeletedAt = _clock.UtcNow;

        // Locked out at the same time, so any path that reads the row while ignoring the
        // deleted marker still refuses the sign-in rather than letting it through.
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await ThrowIfFailedAsync(_users.UpdateAsync(user), subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> SetPasswordAsync(string subject, string password, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result<bool>.Success(false);
        }

        // Through a reset token rather than by removing and re-adding the password. The
        // remove-then-add pair leaves the account with no password at all if the second
        // call fails, and this one is atomic.
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        await ThrowIfFailedAsync(_users.ResetPasswordAsync(user, token, password), subject);
        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<string?>> IssuePasswordResetTokenAsync(string subject, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result<string?>.Success(null);
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        return Result<string?>.Success(token);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ResetPasswordWithTokenAsync(PasswordResetWithToken reset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reset);

        var user = await _users.FindByEmailAsync(reset.Email);
        if (user is null)
        {
            return Result<bool>.Success(false);
        }

        var result = await _users.ResetPasswordAsync(user, reset.Token, reset.NewPassword);
        return Result<bool>.Success(result.Succeeded);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ConfirmEmailWithTokenAsync(EmailConfirmationWithToken confirmation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        var user = await _users.FindByEmailAsync(confirmation.Email);
        if (user is null)
        {
            return Result<bool>.Success(false);
        }

        var result = await _users.ConfirmEmailAsync(user, confirmation.Token);
        return Result<bool>.Success(result.Succeeded);
    }

    /// <inheritdoc/>
    public async Task<Result> ConfirmEmailAsync(string subject, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result.Success();
        }

        user.EmailConfirmed = true;

        await ThrowIfFailedAsync(_users.UpdateAsync(user), subject);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<string?>> IssueEmailConfirmationTokenAsync(string subject, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(subject);
        if (user is null)
        {
            return Result<string?>.Success(null);
        }

        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        return Result<string?>.Success(token);
    }

    /// <summary>
    /// Identity reports a rejected write by returning it, not by throwing. A duplicate
    /// address or a failed validation would otherwise be indistinguishable from success:
    /// the caller commits its own changes and the account keeps the details it had.
    /// </summary>
    /// <param name="update">The pending Identity write.</param>
    /// <param name="subject">Whose account the write was for, for the exception message.</param>
    /// <exception cref="InvalidOperationException">
    /// The write is a genuine infrastructure failure (a duplicate address, a rejected
    /// validation), not a "not allowed here" refusal, so it is not a <see cref="Result"/>: no
    /// caller of this port is prepared to act on it, and it belongs in the log rather than in a
    /// code a frontend would branch on.
    /// </exception>
    /// <returns>A task that completes when the update has succeeded.</returns>
    private static async Task ThrowIfFailedAsync(Task<IdentityResult> update, string subject)
    {
        var result = await update;
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Could not update {subject}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
