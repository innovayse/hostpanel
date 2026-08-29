namespace Innovayse.Application.Reports.Common;

/// <summary>One row of the Clients by City report.</summary>
public record ClientsByCityDto(
    string City,
    string Country,
    int ClientCount);
