namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The company a PDF document is issued by, read from the portal settings.</summary>
/// <param name="Party">The name and the address, identifier and contact lines printed for the seller.</param>
/// <param name="LogoPath">The <c>portal.logo</c> setting as stored; null when none is set.</param>
/// <param name="AccentColour">The brand colour as <c>#rrggbb</c>, dark enough to read on white; null prints in neutral greys.</param>
public sealed record InvoicePdfSeller(InvoicePdfParty Party, string? LogoPath, string? AccentColour);
