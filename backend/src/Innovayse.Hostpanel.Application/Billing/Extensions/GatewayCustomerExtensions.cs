using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>Extension methods for reading a client's accounts at payment gateways.</summary>
public static class GatewayCustomerExtensions
{
    /// <summary>Reads the gateway's reference for the client's account at one gateway module.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="gatewayModuleId">The payment gateway module id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reference, or <see langword="null"/> when the client has no account at that module yet.</returns>
    public static Task<string?> FindGatewayCustomerRefAsync(
        this IHostpanelDbContext context, int clientId, string gatewayModuleId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GatewayCustomers
            .AsNoTracking()
            .Where(g => g.ClientId == clientId && g.GatewayModuleId == gatewayModuleId)
            .Select(g => (string?)g.CustomerRef)
            .FirstOrDefaultAsync(ct);
    }
}
