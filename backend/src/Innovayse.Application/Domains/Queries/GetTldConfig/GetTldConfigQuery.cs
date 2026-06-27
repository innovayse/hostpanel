namespace Innovayse.Application.Domains.Queries.GetTldConfig;

/// <summary>Query to retrieve a single TLD configuration by its identifier.</summary>
/// <param name="Id">The TLD configuration identifier to retrieve.</param>
public record GetTldConfigQuery(int Id);
