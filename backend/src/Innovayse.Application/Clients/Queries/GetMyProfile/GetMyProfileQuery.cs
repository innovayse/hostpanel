namespace Innovayse.Application.Clients.Queries.GetMyProfile;

/// <summary>
/// Query to retrieve the authenticated client's own profile.
/// The controller extracts <paramref name="UserId"/> from the JWT sub claim.
/// </summary>
/// <param name="UserId">The authenticated user's Identity ID.</param>
public record GetMyProfileQuery(string UserId);
