namespace Innovayse.Application.Reports.Common;

/// <summary>Full Direct Debit Processing report result.</summary>
public record DirectDebitDto(IReadOnlyList<DirectDebitRowDto> Rows);
