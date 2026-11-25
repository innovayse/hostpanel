using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Users.Commands.DeleteUser;

/// <summary>
/// Handles <see cref="DeleteUserCommand"/>. An admin cannot delete their own account, and nobody
/// can delete the last active administrator: either would leave the installation with nobody who
/// can manage it.
/// </summary>
public sealed class DeleteUserHandler
{
    /// <summary>Writes people, where this deployment owns them.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Who holds the Admin role.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>Tells which of the holders are still live accounts.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>The data context, where the account's notification read marks are kept.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The acting admin.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Initializes the handler.</summary>
    /// <param name="provisioning">Writes people, where this deployment owns them.</param>
    /// <param name="roles">Who holds the Admin role.</param>
    /// <param name="identity">Tells which of the holders are still live accounts.</param>
    /// <param name="currentUser">The acting admin.</param>
    /// <param name="context">The data context, where the account's notification read marks are kept.</param>
    public DeleteUserHandler(
        IUserProvisioning provisioning,
        ISubjectRoleStore roles,
        IIdentityProvider identity,
        ICurrentUserService currentUser,
        IHostpanelDbContext context)
    {
        _provisioning = provisioning;
        _roles = roles;
        _identity = identity;
        _currentUser = currentUser;
        _context = context;
    }

    /// <summary>Deletes the account. The account's notification read marks are deleted with it; other records that reference the subject are left alone.</summary>
    /// <param name="cmd">The subject to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (whether or not the subject already existed, matching this deployment's own
    /// delete semantics); <c>USER_CANNOT_DELETE_SELF</c> when the subject is the caller;
    /// <c>USER_LAST_ADMIN</c> when the subject is the only active administrator; or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_DELETE_ACCOUNT</c> where an external sign-on provider owns
    /// the accounts.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteUserCommand cmd, CancellationToken ct)
    {
        if (string.Equals(cmd.Id, _currentUser.UserId, StringComparison.Ordinal))
        {
            return Result.FailureCode(HostpanelErrorCodes.USER_CANNOT_DELETE_SELF);
        }

        if (await IsLastActiveAdminAsync(cmd.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.USER_LAST_ADMIN);
        }

        var deleted = await _provisioning.DeleteAsync(cmd.Id, ct);
        if (deleted.IsFailure)
        {
            return deleted;
        }

        // The notification read marks belong to the account, so they go with it; the other records that reference
        // the subject stay.
        var reads = await _context.ActivityReads.Where(r => r.UserId == cmd.Id).ToListAsync(ct);
        if (reads.Count > 0)
        {
            _context.ActivityReads.RemoveRange(reads);
            await _context.SaveChangesAsync(ct);
        }

        return deleted;
    }

    /// <summary>
    /// Tells whether the subject holds Admin and no other live account does. A holder whose account
    /// is deleted or unknown no longer counts: it can sign nobody in.
    /// </summary>
    /// <param name="subject">The subject about to be deleted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when deleting the subject would leave no active administrator.</returns>
    private async Task<bool> IsLastActiveAdminAsync(string subject, CancellationToken ct)
    {
        var holders = await _roles.GetHoldersAsync(Role.Admin, ct);
        if (!holders.Contains(subject, StringComparer.Ordinal))
        {
            return false;
        }

        var others = holders.Where(h => !string.Equals(h, subject, StringComparison.Ordinal)).ToList();
        if (others.Count == 0)
        {
            return true;
        }

        var live = await _identity.GetAccountsBySubjectsAsync(others, ct);
        return live.Count == 0;
    }
}
