namespace Innovayse.Application.Notifications.DTOs;

/// <summary>DTO representing an email template returned by queries.</summary>
/// <param name="Id">The unique identifier of the template.</param>
/// <param name="Slug">The unique slug used to look up the template.</param>
/// <param name="Subject">The email subject line, may contain Liquid variables.</param>
/// <param name="Body">The email body, may contain Liquid variables.</param>
/// <param name="Description">Optional human-readable description of the template's purpose.</param>
/// <param name="IsActive">Whether the template is active and available for sending.</param>
public record EmailTemplateDto(int Id, string Slug, string Subject, string Body, string? Description, bool IsActive);
