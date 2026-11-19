using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Reports.Commands.UpdateDiskUsage;

/// <summary>Handles <see cref="UpdateDiskUsageCommand"/>.</summary>
public sealed class UpdateDiskUsageHandler
{
    /// <summary>Disk and bandwidth usage cache over the hosting servers.</summary>
    private readonly IDiskUsageService _diskService;

    /// <summary>Initializes the handler.</summary>
    /// <param name="diskService">Disk and bandwidth usage cache over the hosting servers.</param>
    public UpdateDiskUsageHandler(IDiskUsageService diskService)
    {
        _diskService = diskService;
    }

    /// <summary>
    /// Fetches fresh figures from every hosting server, replaces the cache, and answers with the
    /// refreshed report so the caller does not need a second round trip to read it.
    /// </summary>
    /// <param name="command">The command. Carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed report, grouped by server. Never refuses: a server that cannot be read is logged and listed in the report's failures.</returns>
    public async Task<Result<DiskUsageDto>> HandleAsync(UpdateDiskUsageCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var report = await _diskService.UpdateNowAsync(ct);
        return Result<DiskUsageDto>.Success(report);
    }
}
