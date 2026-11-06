namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The company a PDF document is issued by, read from the portal settings.</summary>
/// <param name="Party">The name and the address and tax identifier lines printed for the seller.</param>
/// <param name="LogoPath">The <c>portal.logo</c> setting as stored; null when none is set.</param>
public sealed record InvoicePdfSeller(InvoicePdfParty Party, string? LogoPath);
