namespace Innovayse.Application.Clients.Queries.GetMyProfile;

/// <summary>
/// Query to retrieve the authenticated client's own profile.
/// </summary>
/// <remarks>
/// Carries no user id. Whose profile is resolved inside the handler from the credential:
/// a query able to name the subject is a query able to read somebody else's account, and
/// this one is dispatched from six different controllers.
/// </remarks>
public record GetMyProfileQuery();
