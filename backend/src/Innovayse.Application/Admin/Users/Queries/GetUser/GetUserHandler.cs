namespace Innovayse.Application.Admin.Users.Queries.GetUser;

using Innovayse.Application.Admin.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetUserQuery"/>: composes one person's identity record with the client
/// account linked to them, if any.
/// </summary>
/// <param name="identity">Reads people from wherever they live.</param>
/// <param name="clientRepo">Client repository, for the account this person owns.</param>
public sealed class GetUserHandler(IIdentityProvider identity, IClientRepository clientRepo)
{
    /// <summary>Returns the user, or null when the identity provider does not know the subject.</summary>
    /// <param name="query">The subject to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>User detail DTO, or null so the endpoint can answer 404.</returns>
    public async Task<UserDetailDto?> HandleAsync(GetUserQuery query, CancellationToken ct)
    {
        var account = await identity.FindBySubjectAsync(query.Id, ct);
        if (account is null)
        {
            return null;
        }

        var client = await clientRepo.FindByUserIdAsync(query.Id, ct);
        var accounts = client is not null
            ? new List<UserAccountDto>
            {
                new(client.Id, client.FirstName, client.LastName, client.CompanyName, IsOwner: true),
            }
            : [];

        return new UserDetailDto(
            account.Subject, account.FirstName, account.LastName, account.Email,
            // Language lives with whichever store holds the person, and an SSO does not
            // hand it out — left null rather than guessed at.
            Language: null, account.LastLoginAt,
            client?.CreatedAt ?? default, accounts);
    }
}
