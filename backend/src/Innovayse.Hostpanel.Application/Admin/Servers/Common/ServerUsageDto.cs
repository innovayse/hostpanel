namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>What the active services provisioned on a server renew for, as the Servers list shows it.</summary>
/// <param name="ActiveServices">How many active services are provisioned on the server.</param>
/// <param name="MonthlyRevenue">What those services renew for, spread over the months of their cycles, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code of the base currency the revenue is converted to.</param>
public sealed record ServerUsageDto(int ActiveServices, decimal MonthlyRevenue, string Currency);
