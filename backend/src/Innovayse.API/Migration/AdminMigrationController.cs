namespace Innovayse.API.Migration;

using Innovayse.API.Migration.Requests;
using Innovayse.Application.Migration.Commands.CreateMigrationJob;
using Innovayse.Application.Migration.Commands.DeleteMigrationJob;
using Innovayse.Application.Migration.Commands.StartMigrationPull;
using Innovayse.Application.Migration.DTOs;
using Innovayse.Application.Migration.Queries.GetMigrationLogs;
using Innovayse.Application.Migration.Queries.GetMigrationStatus;
using Innovayse.Application.Migration.Queries.ListMigrationJobs;
using Innovayse.Application.Migration.Queries.TestMigrationConnection;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing migration jobs.
/// </summary>
[ApiController]
[Route("api/admin/migrations")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminMigrationController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all migration jobs.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MigrationJobDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<MigrationJobDto>>(
            new ListMigrationJobsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Creates a new migration job and returns the secret key for the migration plugin.</summary>
    [HttpPost]
    public async Task<ActionResult<MigrationJobDto>> CreateAsync(
        [FromBody] CreateMigrationJobRequest req, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<MigrationJobDto>(
            new CreateMigrationJobCommand(
                req.Label,
                req.SourceUrl,
                req.ExportClients,
                req.ExportInvoices,
                req.ExportServices,
                req.ExportDomains,
                req.ExportTickets,
                req.ExportProducts,
                req.ExportOrders,
                req.ExportTransactions,
                req.ExportQuotes,
                req.ExportKnowledgebase,
                req.ExportContacts,
                req.ExportTicketReplies,
                req.ExportAnnouncements,
                req.ExportDownloads,
                req.ExportNetworkIssues), ct);
        return Ok(result);
    }

    /// <summary>Deletes a migration job by ID.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await bus.InvokeAsync(new DeleteMigrationJobCommand(id), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Returns the current status and progress of a migration job.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MigrationJobDto>> GetStatusAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<MigrationJobDto?>(
            new GetMigrationStatusQuery(id), ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>Tests the connection to the migration plugin (panel pulls from plugin).</summary>
    [HttpPost("{id:int}/test-connection")]
    public async Task<ActionResult<MigrationJobDto>> TestConnectionAsync(int id, CancellationToken ct)
    {
        try
        {
            var result = await bus.InvokeAsync<MigrationJobDto>(
                new TestMigrationConnectionQuery(id), ct);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Starts the migration pull process.</summary>
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<MigrationJobDto>> StartAsync(int id, CancellationToken ct)
    {
        try
        {
            var result = await bus.InvokeAsync<MigrationJobDto>(
                new StartMigrationPullCommand(id), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns a paginated import report for a migration job.</summary>
    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<MigrationLogPageDto>> GetLogsAsync(
        int id,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<MigrationLogPageDto>(
            new GetMigrationLogsQuery(id, action, entityType, page, pageSize), ct);
        return Ok(result);
    }
}
