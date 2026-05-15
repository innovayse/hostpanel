namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Support.Commands.CreateDepartment;
using Innovayse.Application.Support.Commands.UpdateDepartment;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetDepartments;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing support departments.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/departments")]
[Authorize(Roles = Roles.Admin)]
public sealed class DepartmentsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all support departments.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all department DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAllAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DepartmentDto>>(new GetDepartmentsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Creates a new support department.</summary>
    /// <param name="request">Department name and email.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new department ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(new CreateDepartmentCommand(request.Name, request.Email), ct);
        return StatusCode(201, id);
    }

    /// <summary>Updates an existing support department.</summary>
    /// <param name="id">Department primary key.</param>
    /// <param name="request">Updated name and email.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateDepartmentCommand(id, request.Name, request.Email), ct);
        return NoContent();
    }
}
