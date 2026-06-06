namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the Direct Debit Processing report.</summary>
public record DirectDebitRowDto(
    int InvoiceId,
    string ClientName,
    string InvoiceDate,
    string DueDate,
    decimal Subtotal,
    decimal Tax,
    decimal Credit,
    decimal Total,
    string? BankName,
    string? BankAccountType,
    string? BankCode,
    string? BankAccountNumber);

/// <summary>Full Direct Debit Processing report result.</summary>
public record DirectDebitDto(IReadOnlyList<DirectDebitRowDto> Rows);
