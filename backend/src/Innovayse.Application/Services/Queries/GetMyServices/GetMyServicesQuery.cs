namespace Innovayse.Application.Services.Queries.GetMyServices;

/// <summary>Returns every service belonging to the calling client.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential.
/// The admin-side read of another client's services is a different use case with its own route.
/// </remarks>
public record GetMyServicesQuery();
