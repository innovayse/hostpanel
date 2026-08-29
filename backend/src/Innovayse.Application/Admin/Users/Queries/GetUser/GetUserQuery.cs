namespace Innovayse.Application.Admin.Users.Queries.GetUser;

/// <summary>Query for a single user with their linked client accounts.</summary>
/// <param name="Id">The person's subject.</param>
public record GetUserQuery(string Id);
