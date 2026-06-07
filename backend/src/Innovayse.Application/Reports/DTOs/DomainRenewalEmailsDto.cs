namespace Innovayse.Application.Reports.DTOs;

/// <summary>One domain renewal reminder email row.</summary>
public record DomainRenewalEmailRowDto(
    string ClientName,
    string Domain,
    DateTimeOffset SentAt,
    string Reminder,
    string Recipients);

/// <summary>Domain renewal reminder emails report.</summary>
public record DomainRenewalEmailsDto(IReadOnlyList<DomainRenewalEmailRowDto> Rows);
