namespace Innovayse.Application.Reports.Queries.GetDomainRenewalEmails;

/// <summary>Query for the Domain Renewal Emails report.</summary>
/// <param name="ClientId">Client to narrow to, or null for every client.</param>
/// <param name="Registrar">Registrar to narrow to, or null for every registrar.</param>
/// <param name="Domain">Domain name to narrow to, or null for every domain.</param>
/// <param name="From">Earliest send date to include.</param>
/// <param name="To">Latest send date to include.</param>
public record GetDomainRenewalEmailsQuery(
    int? ClientId = null,
    string? Registrar = null,
    string? Domain = null,
    DateOnly? From = null,
    DateOnly? To = null);
