using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Authorization;

/// <summary>
/// Resolves client-service ownership against the client account the current credential acts for.
/// </summary>
public sealed class ServiceOwnership : IServiceOwnership
{
    /// <summary>The module's data context, for the service.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for, owner or linked user.</summary>
    private readonly IClientAccess _access;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the service.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user.</param>
    public ServiceOwnership(IHostpanelDbContext context, IClientAccess access)
    {
        _context = context;
        _access = access;
    }

    /// <inheritdoc/>
    public async Task<Result> RequireOwnedByCallerAsync(int serviceId, CancellationToken ct)
    {
        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsSuccess)
        {
            // Read-only, so no tracking is needed.
            var owned = await _context.ClientServices.AsNoTracking()
                .AnyAsync(s => s.Id == serviceId && s.ClientId == clientId.Value, ct);
            if (owned)
            {
                return Result.Success();
            }
        }

        // A service that does not exist, a service belonging to somebody else, and a caller with
        // no account all land here and answer identically. Distinguishing them would turn
        // this route into a way of enumerating ids, and service ids are sequential integers.
        return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> GetOwnedProductIdAsync(int serviceId, CancellationToken ct)
    {
        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsSuccess)
        {
            // One read answers both questions: no row means foreign or missing, and the two answer alike.
            var productId = await _context.ClientServices.AsNoTracking()
                .Where(s => s.Id == serviceId && s.ClientId == clientId.Value)
                .Select(s => (int?)s.ProductId)
                .FirstOrDefaultAsync(ct);
            if (productId is { } product)
            {
                return Result<int>.Success(product);
            }
        }

        return Result<int>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }
}
