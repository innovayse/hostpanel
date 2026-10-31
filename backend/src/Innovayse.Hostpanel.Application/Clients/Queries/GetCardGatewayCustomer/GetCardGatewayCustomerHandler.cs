using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;

/// <summary>Handles <see cref="GetCardGatewayCustomerQuery"/>.</summary>
public sealed class GetCardGatewayCustomerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetCardGatewayCustomerHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Reads the card gateway customer of one client.</summary>
    /// <param name="query">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The link, with a <see langword="null"/> customer id when none was ever saved, or <c>CLIENT_NOT_FOUND</c>.</returns>
    public async Task<Result<CardGatewayCustomerDto>> HandleAsync(GetCardGatewayCustomerQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customer = await _context.Clients
            .AsNoTracking()
            .Where(c => c.Id == query.ClientId)
            .Select(c => new CardGatewayCustomerDto(c.Id, c.CardGatewayCustomerId))
            .FirstOrDefaultAsync(ct);

        return customer is null
            ? Result<CardGatewayCustomerDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND)
            : Result<CardGatewayCustomerDto>.Success(customer);
    }
}
