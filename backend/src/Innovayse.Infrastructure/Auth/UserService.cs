namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implements <see cref="IUserService"/> using ASP.NET Core Identity's <see cref="UserManager{TUser}"/>.
/// </summary>
/// <param name="userManager">The Identity user manager for <see cref="AppUser"/>.</param>
/// <param name="clientRepo">Client repository for account lookups.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
/// <param name="refreshTokenRepo">Refresh token repository for revoking sessions.</param>
/// <param name="revocationCache">In-memory cache for immediate token invalidation.</param>
public sealed class UserService(UserManager<AppUser> userManager, IClientRepository clientRepo, IUnitOfWork uow, IRefreshTokenRepository refreshTokenRepo, TokenRevocationCache revocationCache) : IUserService
{
    /// <inheritdoc/>
    public async Task<string> CreateAsync(string email, string password, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = string.Empty,
            LastName = string.Empty,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        return user.Id;
    }

    /// <inheritdoc/>
    public async Task AddToRoleAsync(string userId, string role, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        await userManager.AddToRoleAsync(user, role);
    }

    /// <inheritdoc/>
    public async Task<(string Id, string Email)?> FindByEmailAndPasswordAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var valid = await userManager.CheckPasswordAsync(user, password);
        return valid ? (user.Id, user.Email!) : null;
    }

    /// <inheritdoc/>
    public async Task<(string Id, string Email)?> FindByIdAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : (user.Id, user.Email!);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetEmailsByIdsAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        foreach (var id in userIds)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user?.Email is not null)
                result[id] = user.Email;
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<List<string>> FindUserIdsByEmailAsync(string emailSearch, CancellationToken ct)
    {
        var term = emailSearch.ToLower();
        return await Task.FromResult(
            userManager.Users
                .Where(u => u.Email != null && u.Email.ToLower().Contains(term))
                .Select(u => u.Id)
                .ToList());
    }

    /// <inheritdoc/>
    public async Task<string?> GetPrimaryRoleAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return roles.FirstOrDefault();
    }

    /// <inheritdoc/>
    public Task<bool> AnyUsersExistAsync(CancellationToken ct)
    {
        return Task.FromResult(userManager.Users.Any());
    }

    /// <inheritdoc/>
    public async Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return false;
        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user?.EmailConfirmed ?? false;
    }

    /// <inheritdoc/>
    public async Task<(List<UserListItemDto> Items, int TotalCount)> ListUsersAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        var query = userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // Batch-lookup linked client IDs
        var userIds = users.Select(u => u.Id).ToList();
        var clientMap = await clientRepo.FindClientIdsByUserIdsAsync(userIds, ct);

        var items = users.Select(u => new UserListItemDto(
            u.Id, clientMap.GetValueOrDefault(u.Id), u.FirstName, u.LastName,
            u.Email!, u.Language, u.LastLoginAt, u.CreatedAt)).ToList();

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<UserDetailDto?> GetUserWithAccountsAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var client = await clientRepo.FindByUserIdAsync(userId, ct);
        var accounts = client is not null
            ? new List<UserAccountDto>
            {
                new(client.Id, client.FirstName, client.LastName, client.CompanyName, true)
            }
            : new List<UserAccountDto>();

        return new UserDetailDto(
            user.Id, user.FirstName, user.LastName, user.Email!,
            user.Language, user.LastLoginAt, user.CreatedAt, accounts);
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(
        string userId, string firstName, string lastName, string email, string? language, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.UserName = email;
        user.Language = language;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update user: {errors}");
        }
    }

    /// <inheritdoc/>
    public async Task DeleteUserAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        // Close the linked client account before deleting the user
        var client = await clientRepo.FindByUserIdAsync(userId, ct);
        if (client is not null)
        {
            client.Close();
            await uow.SaveChangesAsync(ct);
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete user: {errors}");
        }
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePasswordResetTokenAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(string userId, string newPassword, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to change password: {errors}");
        }

        await refreshTokenRepo.RevokeAllForUserAsync(userId, ct);
        revocationCache.RevokeUser(userId);
    }

    /// <inheritdoc/>
    public async Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return false;

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            await refreshTokenRepo.RevokeAllForUserAsync(user.Id, ct);
            revocationCache.RevokeUser(user.Id);
        }

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task UpdateLastLoginAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
    }
}
