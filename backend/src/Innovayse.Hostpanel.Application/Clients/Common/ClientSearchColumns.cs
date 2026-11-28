using System.Collections.Immutable;
using System.Linq.Expressions;
using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// The one definition of what a search over clients looks at on the client row, shared by the clients list and by the
/// query other modules ask for the clients a search text names, so the two never drift apart.
/// </summary>
public static class ClientSearchColumns
{
    /// <summary>
    /// The text columns of a client row a search looks in: first name, last name, the full name as it is read
    /// ("First Last", which neither column alone contains) and the company. The sign-in address and account name
    /// are not on the row and are searched through the identity provider.
    /// </summary>
    public static readonly ImmutableArray<Expression<Func<Client, string?>>> All =
    [
        c => c.FirstName,
        c => c.LastName,
        c => c.FirstName + " " + c.LastName,
        c => c.CompanyName,
    ];
}
