namespace Innovayse.Application.Reports.Common;

/// <summary>One domain renewal reminder email row.</summary>
public record DomainRenewalEmailRowDto(
    string ClientName,
    string Domain,
    DateTimeOffset SentAt,
    string Reminder,
    string Recipients);
