using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// Lookups over the client groups that more than one use case needs: whether the group an id points at
/// exists, and which group new clients join. A handler that stores a group id checks it here before saving, so a
/// missing group is refused with <c>CLIENT_GROUP_NOT_FOUND</c> instead of surfacing as a failed save.
/// </summary>
public static class ClientGroupLookupExtensions
{
    /// <summary>Whether a client group with this id exists.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientGroupId">The id to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the group exists.</returns>
    public static Task<bool> ClientGroupExistsAsync(this IHostpanelDbContext context, int clientGroupId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ClientGroups.AsNoTracking().AnyAsync(g => g.Id == clientGroupId, ct);
    }

    /// <summary>The id of the group new clients join, when one is set.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The default group's id, or <see langword="null"/> when no group is the default.</returns>
    public static Task<int?> FindDefaultClientGroupIdAsync(this IHostpanelDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ClientGroups.AsNoTracking()
            .Where(g => g.IsDefault)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Puts a new client in the default group, when there is one. Every path that creates a client
    /// without a group of its own (registering, guest checkout, a signed-in first order) goes through here.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="client">The client that has just been created and not yet saved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the client has been placed, or left ungrouped when no group is the default.</returns>
    public static async Task AssignDefaultClientGroupAsync(this IHostpanelDbContext context, Client client, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(client);

        if (await context.FindDefaultClientGroupIdAsync(ct) is { } defaultGroupId)
        {
            client.AssignToGroup(defaultGroupId);
        }
    }
}
