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
        string email, string? firstName, string? lastName, CancellationToken ct)
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

        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }

    /// <inheritdoc/>
    public async Task UpdateNameAsync(
        string subject, string firstName, string lastName, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(subject);
        if (user is null) return;

        user.FirstName = firstName;
        user.LastName = lastName;
        await users.UpdateAsync(user);
    }
}
