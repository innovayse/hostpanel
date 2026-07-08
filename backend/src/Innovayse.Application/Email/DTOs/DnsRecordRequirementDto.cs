namespace Innovayse.Application.Email.DTOs;

/// <summary>
/// Represents a single required DNS record for an email domain,
/// optionally decorated with live verification results.
/// </summary>
public record DnsRecordRequirementDto(
    string Type,
    string Name,
    string Value,
    int? Priority,
    bool? Verified,
    string? FoundValue,
    string? Error);
