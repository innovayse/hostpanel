namespace Innovayse.Application.Domains.Commands.ImportTldPricing;

/// <summary>Command to bulk-import TLD pricing from a registrar provider.</summary>
/// <param name="Module">The registrar module name to import from (e.g. "NameAm", "Namecheap").</param>
public record ImportTldPricingCommand(string Module);

/// <summary>Result of a TLD pricing import operation.</summary>
/// <param name="Imported">Number of new TLD configurations created during the import.</param>
/// <param name="Updated">Number of existing TLD configurations updated with new cost prices.</param>
public record ImportTldPricingResult(int Imported, int Updated);
