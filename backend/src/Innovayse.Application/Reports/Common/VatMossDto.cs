namespace Innovayse.Application.Reports.Common;

/// <summary>VAT MOSS Settlement Data report result.</summary>
public record VatMossDto(
    string PeriodLabel,
    IReadOnlyList<VatMossRowDto> Rows);
