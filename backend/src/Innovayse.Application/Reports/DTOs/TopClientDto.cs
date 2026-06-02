namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Top 10 Clients by Income report.</summary>
public record TopClientDto(
    int Rank,
    int ClientId,
    string ClientName,
    decimal TotalIncome,
    int InvoiceCount,
    decimal AvgInvoice);
