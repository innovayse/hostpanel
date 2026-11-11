using Innovayse.Hostpanel.Application.Migration.Commands.CreateMigrationJob;
using Innovayse.Hostpanel.Application.Migration.Commands.DeleteMigrationJob;
using Innovayse.Hostpanel.Application.Migration.Commands.RetryMigrationJob;
using Innovayse.Hostpanel.Application.Migration.Commands.StartMigrationPull;
using Innovayse.Hostpanel.Application.Migration.Commands.TestMigrationConnection;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Queries.GetMigrationLogs;
using Innovayse.Hostpanel.Application.Migration.Queries.GetMigrationStatus;
using Innovayse.Hostpanel.Application.Migration.Queries.ListMigrationJobs;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Migration jobs that pull an operator's data out of the legacy system this deployment replaces.
/// </summary>
[Route("api/admin/migrations")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MigrationsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MigrationsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Every migration job, most recently created first.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the jobs; 401 without a session; 403 for a caller who is not staff; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MigrationJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<MigrationJobDto>>>(new ListMigrationJobsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new pending migration job and its shared secret key.</summary>
    /// <param name="command">The source and the entity types to export from it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the job; 400 when the source address is not usable; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MigrationJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMigrationJobCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MigrationJobDto>>(command, ct);
        return HandleResult(outcome, job => CreatedResult($"/api/admin/migrations/{job.Id}", job));
    }

    /// <summary>Deletes a migration job the operator no longer wants to keep a record of.</summary>
    /// <param name="id">The job to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when deleted; 404 when the id names no job; 401 without a session; 403 for a caller who is not an admin; 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteMigrationJobCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>The current status and progress of one migration job.</summary>
    /// <param name="id">The job to report on.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the job; 404 when the id names no job; 401 without a session; 403 for a caller who is not an admin; 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MigrationJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatusAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MigrationJobDto>>(new GetMigrationStatusQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Tests the connection from this panel to the legacy system's migration plugin.</summary>
    /// <param name="id">The job whose connection to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the job once the plugin has answered; 404 when the id names no job; 502 when the source could not be reached; 401 without a session; 403 for a caller who is not an admin; 400 when the request is refused;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/test-connection")]
    [ProducesResponseType(typeof(MigrationJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnectionAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MigrationJobDto>>(new TestMigrationConnectionCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Starts pulling data from the legacy system into this panel, in the background.</summary>
    /// <param name="id">The pending job to start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the job once its pull is queued; 400 when it is not <c>Pending</c>, or a concurrent
    /// start queued it first; 404 when the id names no job; 401 without a session; 403 for a caller
    /// without access; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/start")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(MigrationJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MigrationJobDto>>(new StartMigrationPullCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Queues a failed job's pull again, in the background.</summary>
    /// <param name="id">The failed job to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the job once its pull is queued; 400 when it has not failed, or a concurrent retry
    /// queued it first; 404 when the id names no job; 401 without a session; 403 for a caller
    /// without access; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/retry")]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(MigrationJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RetryAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MigrationJobDto>>(new RetryMigrationJobCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>One page of a migration job's import report.</summary>
    /// <param name="id">The job whose report to read.</param>
    /// <param name="query">The optional filters and the page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page, empty when the id names no job; 400 when a filter names no member of its
    /// enum; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/logs")]
    [ProducesResponseType(typeof(PaginatedList<MigrationLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLogsAsync(int id, [FromQuery] GetMigrationLogsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<MigrationLogDto>>>(query with { JobId = id }, ct);
        return HandleResult(outcome);
    }
}
