namespace Innovayse.Application.Audit.DTOs;

/// <summary>DTO representing a single client activity log entry.</summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Description">Human-readable description of the action.</param>
/// <param name="AdminName">Display name of the admin who performed the action.</param>
/// <param name="AdminEmail">Email of the admin who performed the action.</param>
/// <param name="IpAddress">IP address from which the action originated.</param>
/// <param name="CreatedAt">UTC timestamp of the action.</param>
public record ActivityLogDto(
    int Id,
    string Description,
    string? AdminName,
    string? AdminEmail,
    string? IpAddress,
    DateTimeOffset CreatedAt);
