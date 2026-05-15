namespace Innovayse.API.Admin;
using Innovayse.API.Admin.Requests;
using Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;
using Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.Application.Admin.Plugins.Queries.ListPlugins;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing installable provider plugins.
/// All write operations return <c>requiresRestart: true</c> — the client must trigger a restart
/// via <see cref="Restart"/> before changes take effect.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="lifetime">Host lifetime used to trigger graceful restart.</param>
[ApiController]
[Route("api/admin/plugins")]
[Authorize(Roles = Roles.Admin)]
public sealed class PluginsController(IMessageBus bus, IHostApplicationLifetime lifetime) : ControllerBase
{
    /// <summary>
    /// Returns all installed plugins with their load status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of installed plugin summary items.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PluginListItemDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<PluginListItemDto>>(
            new ListPluginsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Installs a plugin from an uploaded ZIP archive.
    /// </summary>
    /// <param name="req">Multipart form data containing the ZIP file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>400 on validation error; 200 with <c>requiresRestart: true</c> on success.</returns>
    [HttpPost("install")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PluginActionResultDto>> InstallAsync(
        [FromForm] InstallPluginRequest req,
        CancellationToken ct)
    {
        if (req.File.Length == 0)
        {
            return BadRequest("Uploaded file is empty.");
        }

        if (!req.File.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .zip files are accepted.");
        }

        byte[] zipBytes;
        using (var ms = new MemoryStream())
        {
            await req.File.CopyToAsync(ms, ct);
            zipBytes = ms.ToArray();
        }

        try
        {
            var result = await bus.InvokeAsync<PluginActionResultDto>(
                new InstallPluginCommand(zipBytes), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes an installed plugin by deleting its files from disk.
    /// </summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>404 when not found; 200 with <c>requiresRestart: true</c> on success.</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult<PluginActionResultDto>> RemoveAsync(string id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PluginActionResultDto?>(
            new RemovePluginCommand(id), ct);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Triggers a graceful application restart so newly installed or removed plugins take effect.
    /// The process manager (systemd, Docker restart policy) brings the process back up.
    /// </summary>
    /// <returns>200 with a restart status message.</returns>
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        lifetime.StopApplication();
        return Ok(new { message = "Server is restarting. Poll /api/health until it responds." });
    }
}
