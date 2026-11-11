using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClient;

/// <summary>Returns full client details including contacts and email from Identity.</summary>
public sealed class GetClientHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the person this client belongs to.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the person this client belongs to.</param>
    public GetClientHandler(IHostpanelDbContext context, IIdentityProvider identity)
    {
        _context = context;
        _identity = identity;
    }

    /// <summary>Retrieves a client by id and maps it to <see cref="ClientDto"/>.</summary>
    /// <param name="query">The query with the client id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result<ClientDto>> HandleAsync(GetClientQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _context.Clients
            .Include(c => c.Contacts)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.ClientId, ct);
        if (client is null)
        {
            return Result<ClientDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var group = client.ClientGroupId is { } groupId
            ? await _context.ClientGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct)
            : null;

        var user = await _identity.FindBySubjectAsync(client.UserId, ct);
        var email = user?.Email ?? "";

        return Result<ClientDto>.Success(client.ToDto(group, email, user?.TwoFactorEnabled ?? false, user?.Language));
    }
}
