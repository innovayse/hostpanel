namespace Innovayse.API.Admin;

using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Commands.UpdateSetting;
using Innovayse.Application.Admin.DTOs;
using Innovayse.Application.Admin.Queries.GetSetting;
using Innovayse.Application.Admin.Queries.GetSettings;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing system configuration settings.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = Roles.Admin)]
public sealed class SettingsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all configuration settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all settings.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> GetAllAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<SettingDto>>(new GetSettingsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single setting by ID.</summary>
    /// <param name="id">Setting primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Setting DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SettingDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<SettingDto>(new GetSettingQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Updates the value of an existing setting.</summary>
    /// <param name="id">Setting primary key.</param>
    /// <param name="request">Request body containing the new value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateSettingCommand(id, request.Value), ct);
        return NoContent();
    }
}
