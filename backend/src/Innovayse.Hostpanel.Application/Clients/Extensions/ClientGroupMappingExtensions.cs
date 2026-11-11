using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// Maps <see cref="ClientGroup"/> to <see cref="ClientGroupDto"/>. The list and the single read both
/// project through this one mapping, so a field added to the DTO is added once.
/// </summary>
public static class ClientGroupMappingExtensions
{
    /// <summary>Projects the query to <see cref="ClientGroupDto"/> rows, counting each group's clients in the same SQL statement.</summary>
    /// <param name="source">The groups to project.</param>
    /// <param name="clients">The clients the member count is taken from.</param>
    /// <returns>The query projected to <see cref="ClientGroupDto"/>.</returns>
    public static IQueryable<ClientGroupDto> SelectDto(this IQueryable<ClientGroup> source, IQueryable<Client> clients)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clients);

        return source.Select(g => new ClientGroupDto(
            g.Id, g.Name, g.Description, g.DiscountPercent, g.Color, g.IsDefault,
            clients.Count(c => c.ClientGroupId == g.Id)));
    }
}
