using Innovayse.Hostpanel.Application.Clients.Commands.CreateClientGroup;
using Innovayse.Hostpanel.Application.Clients.Commands.DeleteClientGroup;
using Innovayse.Hostpanel.Application.Clients.Commands.SetDefaultClientGroup;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClientGroup;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientGroup;
using Innovayse.Hostpanel.Application.Clients.Queries.ListClientGroups;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin API for client groups: the named groups staff put clients into, each with a discount and a colour.</summary>
[Route("api/admin/client-groups")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientGroupsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientGroupsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Every client group, with how many clients belong to each.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the groups; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClientGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientGroupDto>>>(new ListClientGroupsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>A single client group.</summary>
    /// <param name="id">The group's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the group; 404 with <c>CLIENT_GROUP_NOT_FOUND</c>; 400 when the route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientGroupDto>>(new GetClientGroupQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a client group. A new group is not the default.</summary>
    /// <param name="command">The group's name, description, discount and colour.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new group's id; 409 with <c>CLIENT_GROUP_NAME_ALREADY_EXISTS</c> when another group has the name; 400 when a field is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateClientGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/client-groups/{id}", id));
    }

    /// <summary>Replaces a client group's name, description, discount and colour.</summary>
    /// <param name="id">The group's primary key; wins over any id in the body.</param>
    /// <param name="command">The group's new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>CLIENT_GROUP_NOT_FOUND</c>; 409 with <c>CLIENT_GROUP_NAME_ALREADY_EXISTS</c> when another group has the name; 400 when a field is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateClientGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Makes a client group the default for new clients, clearing the flag on the group that held it, or clears the flag.</summary>
    /// <param name="id">The group's primary key; wins over any id in the body.</param>
    /// <param name="command">Whether the group is to be the default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>CLIENT_GROUP_NOT_FOUND</c>; 400 when the body or route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPut("{id:int}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultAsync(int id, [FromBody] SetDefaultClientGroupCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a client group that has no clients.</summary>
    /// <param name="id">The group's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>CLIENT_GROUP_NOT_FOUND</c>; 409 with <c>CLIENT_GROUP_IN_USE</c> while a client still belongs to it; 400 when the route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteClientGroupCommand(id), ct);
        return HandleResult(outcome);
    }
}
