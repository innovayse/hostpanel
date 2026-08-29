namespace Innovayse.Application.Domains.Commands.ImportTldPricing;

/// <summary>Command to bulk-import TLD pricing from a registrar provider.</summary>
/// <param name="Module">The registrar module name to import from (e.g. "NameAm", "Namecheap").</param>
public record ImportTldPricingCommand(string Module);
