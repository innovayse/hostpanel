namespace Innovayse.API.Notifications;

using Innovayse.API.Notifications.Requests;
using Innovayse.Application.Notifications.Commands.CreateEmailTemplate;
using Innovayse.Application.Notifications.Commands.DeleteEmailTemplate;
using Innovayse.Application.Notifications.Commands.ToggleEmailTemplate;
using Innovayse.Application.Notifications.Commands.UpdateEmailTemplate;
using Innovayse.Application.Notifications.DTOs;
using Innovayse.Application.Notifications.Queries.GetEmailTemplate;
using Innovayse.Application.Notifications.Queries.ListEmailTemplates;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing email templates.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/email-templates")]
[Authorize(Roles = Roles.Admin)]
public sealed class EmailTemplatesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all registered email templates.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of email template DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailTemplateDto>>> GetAllAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<EmailTemplateDto>>(new ListEmailTemplatesQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single email template by its ID.</summary>
    /// <param name="id">Email template primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching email template DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailTemplateDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<EmailTemplateDto>(new GetEmailTemplateQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new email template.</summary>
    /// <param name="req">The create request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new template ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateEmailTemplateRequest req, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateEmailTemplateCommand(req.Slug, req.Subject, req.Body, req.Description), ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id }, id);
    }

    /// <summary>Updates the content of an existing email template.</summary>
    /// <param name="id">Email template primary key.</param>
    /// <param name="req">The update request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateEmailTemplateRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateEmailTemplateCommand(id, req.Subject, req.Body, req.Description), ct);
        return NoContent();
    }

    /// <summary>Deletes an email template.</summary>
    /// <param name="id">Email template primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteEmailTemplateCommand(id), ct);
        return NoContent();
    }

    /// <summary>Toggles the active status of an email template.</summary>
    /// <param name="id">Email template primary key.</param>
    /// <param name="req">The toggle request body containing the desired active state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> ToggleAsync(int id, [FromBody] ToggleEmailTemplateRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new ToggleEmailTemplateCommand(id, req.Active), ct);
        return NoContent();
    }
}
