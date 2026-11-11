using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>A plan a service can move to, with what the move costs or gives back, worked out before the client confirms.</summary>
/// <param name="ProductId">The plan's product.</param>
/// <param name="Name">The plan's display name.</param>
/// <param name="Type">
/// Whether the move is an upgrade or a downgrade. Sent as the member name, not display text: the caller owns the
/// wording, in its own language.
/// </param>
/// <param name="NewRecurringAmount">The plan's price per billing cycle, in the client's billing currency.</param>
/// <param name="ProrationAmount">
/// The prorated difference for the rest of the current cycle, never negative: what the client is charged for an upgrade,
/// what is credited to their balance for a downgrade.
/// </param>
/// <param name="CurrencyCode">ISO 4217 alpha code of the client's billing currency.</param>
public sealed record ServicePlanChangeOptionDto(
    int ProductId,
    string Name,
    ServiceChangeType Type,
    decimal NewRecurringAmount,
    decimal ProrationAmount,
    string CurrencyCode);
