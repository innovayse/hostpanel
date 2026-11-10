using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyServiceInvoices;

/// <summary>
/// Returns the invoices charged to one of the calling client's own services, refusing every
/// service that is not theirs.
/// </summary>
/// <remarks>
/// <para>
/// The check lives here rather than at the endpoint, so it travels with the message. That
/// matters more than usual for this query: it reads financial records, and the id it is given
/// comes straight off a route whose sibling actions take the same kind of id and check nothing.
/// </para>
/// <para>
/// Ownership is settled twice over, deliberately. The service must belong to the caller, and the
/// invoice read is <i>also</i> scoped to that caller's client id rather than fetched by service
/// alone, so a service id that somehow passed the first check still cannot return another
/// account's invoices. Neither condition is load-bearing on its own being enough.
/// </para>
/// </remarks>
public sealed class GetMyServiceInvoicesHandler
{
    /// <summary>The rule that says a client may only look at their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Resolves the account the caller acts for, owner or linked user.</summary>
    private readonly IClientAccess _access;

    /// <summary>The module's data context, for the invoices charged to the service.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only look at their own services.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user.</param>
    /// <param name="context">The module's data context, for the invoices charged to the service.</param>
    public GetMyServiceInvoicesHandler(IServiceOwnership ownership, IClientAccess access, IHostpanelDbContext context)
    {
        _ownership = ownership;
        _access = access;
        _context = context;
    }

    /// <summary>Handles <see cref="GetMyServiceInvoicesQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The invoices recorded against the service, and how many of the caller's invoices are
    /// recorded against no service at all, or <c>SERVICE_NOT_FOUND</c> when the service is not
    /// the caller's, when no such service exists, or when the caller acts for no account.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<ServiceInvoicesDto>> HandleAsync(GetMyServiceInvoicesQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<ServiceInvoicesDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var clientIdOutcome = await _access.RequireClientIdAsync(ct);
        if (clientIdOutcome.IsFailure)
        {
            return Result<ServiceInvoicesDto>.FailureCode(clientIdOutcome.ErrorCode!);
        }

        var clientId = clientIdOutcome.Value;

        // Read-only, so no tracking is needed.
        var linked = await _context.Invoices
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Transactions)
            .Where(Invoice.ClientVisibleFilter)
            .Where(x => x.ClientId == clientId && x.Items.Any(i => i.ClientServiceId == query.ServiceId))
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync(ct);
        var unattributed = await _context.Invoices
            .Where(Invoice.ClientVisibleFilter)
            .Where(x => x.ClientId == clientId && !x.Items.Any(i => i.ClientServiceId != null))
            .CountAsync(ct);

        return Result<ServiceInvoicesDto>.Success(new ServiceInvoicesDto(
            linked.Select(InvoiceMapper.MapToMyDto).ToList(),
            unattributed));
    }
}
