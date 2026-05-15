namespace Innovayse.Application.Admin.DTOs;

/// <summary>
/// Represents the usage count of a specific product across all client services.
/// </summary>
/// <param name="ProductName">Display name of the product.</param>
/// <param name="Count">Number of active client service instances for this product.</param>
public record ServiceUsageReportItemDto(string ProductName, int Count);
