using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListMySshKeys;

/// <summary>
/// Lists a service's keys from HostPanel's records and, when the server answers, marks which of
/// them the server actually holds. The records say what was asked for; the server says what can
/// log in. Both are shown rather than one silently overwriting the other.
/// </summary>
public sealed class ListMySshKeysHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Resolves server, provider and account once ownership is settled.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger.</summary>
    private readonly ILogger<ListMySshKeysHandler> _logger;

    /// <summary>Initialises the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="resolver">Resolves server, provider and account once ownership is settled.</param>
    /// <param name="context">Data context.</param>
    /// <param name="logger">Records a provider failure without putting its detail on the wire.</param>
    public ListMySshKeysHandler(
        IServiceOwnership ownership, ISshServiceContextResolver resolver,
        IHostpanelDbContext context, ILogger<ListMySshKeysHandler> logger)
    {
        _ownership = ownership;
        _resolver = resolver;
        _context = context;
        _logger = logger;
    }

    /// <summary>Handles <see cref="ListMySshKeysQuery"/>.</summary>
    /// <param name="query">Names the caller's service whose keys are listed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The keys, oldest first; <c>SERVICE_NOT_FOUND</c> when the service is not the caller's;
    /// <c>SSH_ACCESS_UNAVAILABLE</c> when the service is not active.
    /// </returns>
    public async Task<Result<IReadOnlyList<SshKeyDto>>> HandleAsync(ListMySshKeysQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<IReadOnlyList<SshKeyDto>>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var context = await _resolver.ResolveAsync(query.ServiceId, ct);
        if (context is null)
        {
            return Result<IReadOnlyList<SshKeyDto>>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (context.State is SshAccessState.ServiceInactive)
        {
            return Result<IReadOnlyList<SshKeyDto>>.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        var records = await _context.SshKeys.AsNoTracking()
            .Where(k => k.ClientServiceId == context.Service.Id && k.SyncState != SshKeySyncState.Revoked)
            .OrderBy(k => k.CreatedAt)
            .ToListAsync(ct);

        HashSet<Guid>? onServer = null;
        if (context.State is SshAccessState.Available && records.Count > 0)
        {
            try
            {
                var installed = await context.Provider!.ListKeysAsync(context.Account!, ct);
                onServer = installed.Select(k => k.KeyId).ToHashSet();
            }
            catch (SshProviderException ex)
            {
                _logger.LogWarning(ex, "SSH key listing for service {ServiceId} failed: {Failure}", context.Service.Id, ex.Failure);
            }
        }

        IReadOnlyList<SshKeyDto> keys = records
            .Select(k =>
            {
                // Presence is only meaningful for keys bound to the server that was asked.
                bool? present = onServer is not null && k.IsBoundTo(context.Account!.ServerId, context.Account.Username)
                    ? onServer.Contains(k.KeyId)
                    : null;
                return k.ToDto(context, present);
            })
            .ToList();
        return Result<IReadOnlyList<SshKeyDto>>.Success(keys);
    }
}
