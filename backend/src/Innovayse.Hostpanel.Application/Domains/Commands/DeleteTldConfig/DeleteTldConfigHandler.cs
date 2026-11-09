using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteTldConfig;

/// <summary>
/// Handles <see cref="DeleteTldConfigCommand"/> by loading the TLD configuration,
/// removing it from the data context, and persisting the deletion.
/// Invalidates the TLD pricing cache after deletion.
/// </summary>
public sealed class DeleteTldConfigHandler
{
    /// <summary>The data context the configuration is loaded from and removed through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Memory cache for invalidating pricing data.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the configuration is loaded from and removed through.</param>
    /// <param name="cache">Memory cache for invalidating pricing data.</param>
    public DeleteTldConfigHandler(IHostpanelDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Deletes a TLD configuration permanently from the persistence store.
    /// </summary>
    /// <param name="cmd">The delete command containing the TLD configuration ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the configuration is deleted, or <c>TLD_CONFIG_NOT_FOUND</c> (404) when the
    /// id names no configuration.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteTldConfigCommand cmd, CancellationToken ct)
    {
        var entity = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (entity is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
        }

        _context.TldConfigs.Remove(entity);
        await _context.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients no longer see the deleted TLD.
        _cache.Remove("tld-pricing-configs");

        return Result.Success();
    }
}
