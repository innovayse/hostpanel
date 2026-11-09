using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Looks up the local <c>Client.Id</c> for a plugin record's email address. It exists so every importer that needs to resolve a client shares
/// the same lookup rather than repeating it.
/// </summary>
public sealed class MigrationClientResolver
{
    /// <summary>Resolves the plugin's email address to this platform's own identity.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>The module's data context, for mapping the resolved identity to its client row.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes a new instance of the <see cref="MigrationClientResolver"/> class.</summary>
    /// <param name="identity">Resolves the plugin's email address to this platform's own identity.</param>
    /// <param name="context">The module's data context, for mapping the resolved identity to its client row.</param>
    public MigrationClientResolver(IIdentityProvider identity, IHostpanelDbContext context)
    {
        _identity = identity;
        _context = context;
    }

    /// <summary>
    /// Resolves <paramref name="email"/> to the local client it belongs to, if any.
    /// </summary>
    /// <param name="email">The email address as the source plugin recorded it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The matching client's id, or <see langword="null"/> when no identity account or no client
    /// row exists for that email.
    /// </returns>
    public async Task<int?> ResolveClientIdAsync(string email, CancellationToken ct)
    {
        var user = await _identity.FindByEmailAsync(email, ct);
        if (user is null)
        {
            return null;
        }

        var client = await _context.Clients
            .IncludeChildren()
            .FirstOrDefaultAsync(c => c.UserId == user.Subject, ct);
        return client?.Id;
    }
}
