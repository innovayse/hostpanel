using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>What changing a service's plan mid-cycle costs the client or gives back to them.</summary>
/// <param name="Type">Whether the new plan is dearer (an upgrade) or cheaper (a downgrade).</param>
/// <param name="Amount">
/// The prorated difference in the client's billing currency, rounded to its minor units and never negative:
/// the charge of an upgrade, the credit of a downgrade. Zero when no time is left in the cycle.
/// </param>
public sealed record ProratedPlanChange(ServiceChangeType Type, decimal Amount);
