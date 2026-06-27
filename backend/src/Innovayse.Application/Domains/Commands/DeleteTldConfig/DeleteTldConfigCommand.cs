namespace Innovayse.Application.Domains.Commands.DeleteTldConfig;

/// <summary>Command to permanently delete a TLD configuration by ID.</summary>
/// <param name="Id">The identifier of the TLD configuration to delete.</param>
public record DeleteTldConfigCommand(int Id);
