namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>What the active services provisioned on one server renew for, in the base currency.</summary>
/// <param name="ActiveServices">How many active services are provisioned on the server.</param>
/// <param name="Monthly">What those services renew for, spread over the months of their cycles; not yet rounded, so a caller rounds once after any further arithmetic.</param>
public sealed record ServerRevenue(int ActiveServices, decimal Monthly);
