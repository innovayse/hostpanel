namespace Innovayse.Hostpanel.Application.Domains.Commands.ImportTldPricing;

/// <summary>Command to bulk-import TLD pricing from a registrar provider.</summary>
/// <param name="Module">The registrar module to import from, the id of an installed registrar plugin, in any letter case.</param>
public sealed record ImportTldPricingCommand(string Module);
