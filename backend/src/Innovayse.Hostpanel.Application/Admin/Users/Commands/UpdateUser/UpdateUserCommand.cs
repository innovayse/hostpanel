using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Users.Commands.UpdateUser;

/// <summary>Command to update a user's profile and sign-in address (admin action).</summary>
/// <param name="Id">
/// The person's subject. Nullable because the controller overlays it from the route
/// (<c>command with { Id = id }</c>): a non-nullable member of a bound body is implicitly
/// required, so the request would be refused before the overlay could run.
/// </param>
/// <param name="FirstName">New first name.</param>
/// <param name="LastName">New last name.</param>
/// <param name="Email">New email address.</param>
/// <param name="Language">Preferred language code (en, ru, hy, de, es) or null for default.</param>
public sealed record UpdateUserCommand(
    string? Id,
    string FirstName,
    string LastName,
    string Email,
    string? Language) : IDemoRestricted;
