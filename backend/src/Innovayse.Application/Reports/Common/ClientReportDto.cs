namespace Innovayse.Application.Reports.Common;

/// <summary>One row in the Clients report.</summary>
public record ClientReportDto(
    int Id,
    string FirstName,
    string LastName,
    string? CompanyName,
    string Email,
    string? Address1,
    string? Address2,
    string? City,
    string? State,
    string? PostCode,
    string? Country,
    string? Phone,
    string? Currency,
    decimal CreditBalance,
    string Status,
    string CreatedAt,
    string? Notes);
