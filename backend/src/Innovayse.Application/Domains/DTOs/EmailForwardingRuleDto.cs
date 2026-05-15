namespace Innovayse.Application.Domains.DTOs;

/// <summary>DTO for an email forwarding rule.</summary>
/// <param name="Id">Rule primary key.</param>
/// <param name="Source">Source alias or local part.</param>
/// <param name="Destination">Destination email address.</param>
/// <param name="IsActive">Whether the rule is currently active.</param>
public record EmailForwardingRuleDto(int Id, string Source, string Destination, bool IsActive);
