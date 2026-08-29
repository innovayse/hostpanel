namespace Innovayse.Application.Reports.Commands.UpdateDiskUsage;

using Innovayse.Application.Reports.Common;
using Innovayse.Application.Reports.Interfaces;

/// <summary>Handles <see cref="UpdateDiskUsageCommand"/>.</summary>
/// <param name="diskService">Disk and bandwidth usage cache over the hosting servers.</param>
public sealed class UpdateDiskUsageHandler(IDiskUsageService diskService)
{
    /// <summary>
    /// Fetches fresh figures from every hosting server, replaces the cache, and answers with the
    /// refreshed report so the caller does not need a second round trip to read it.
    /// </summary>
    /// <param name="command">The command. Carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed report, grouped by server.</returns>
    public Task<DiskUsageDto> HandleAsync(UpdateDiskUsageCommand command, CancellationToken ct)
        => diskService.UpdateNowAsync(ct);
}
