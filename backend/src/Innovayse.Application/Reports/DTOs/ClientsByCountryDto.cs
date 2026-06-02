namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Clients by Country report.</summary>
public record ClientsByCountryDto(
    string Country,
    int ClientCount,
    decimal TotalRevenue);
