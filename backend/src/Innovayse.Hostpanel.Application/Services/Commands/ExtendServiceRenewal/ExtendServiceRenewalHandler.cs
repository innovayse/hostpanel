using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Commands.ExtendServiceRenewal;

/// <summary>Handles <see cref="ExtendServiceRenewalCommand"/>: load, extend, save.</summary>
public sealed class ExtendServiceRenewalHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ExtendServiceRenewalHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Extends the service.</summary>
    /// <param name="cmd">The service and the paid period's end.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SERVICE_NOT_FOUND</c>.</returns>
    public async Task<Result> HandleAsync(ExtendServiceRenewalCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        service.ExtendRenewalTo(cmd.PeriodEnd);
        service.CompleteTrialConversion();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
