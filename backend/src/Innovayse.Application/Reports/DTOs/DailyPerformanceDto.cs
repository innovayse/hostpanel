namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row of the Daily Performance report.</summary>
public record DailyPerformanceDto(
    string Date,
    int CompletedOrders,
    int NewInvoices,
    int PaidInvoices,
    int FailedGateways,
    int TicketReplies,
    int Cancellations);
