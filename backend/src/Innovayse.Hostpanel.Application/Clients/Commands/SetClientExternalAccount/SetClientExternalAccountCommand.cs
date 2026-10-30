namespace Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;

/// <summary>
/// Command to set or clear the link between a client and an account in another system. Staff only:
/// the client's own profile update cannot reach it.
/// </summary>
/// <param name="ClientId">The client's primary key; the route wins over any value in the body.</param>
/// <param name="Provider">The name of the other system, or <see langword="null"/> (with a null <paramref name="AccountId"/>) to clear the link.</param>
/// <param name="AccountId">The identifier the other system gives the client, or <see langword="null"/> (with a null <paramref name="Provider"/>) to clear the link.</param>
public sealed record SetClientExternalAccountCommand(int ClientId, string? Provider, string? AccountId);
