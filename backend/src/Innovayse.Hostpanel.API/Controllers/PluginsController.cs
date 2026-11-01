using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallPlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPlugins;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Staff administration of installable provider plugins. Every write answers
/// <c>requiresRestart: true</c>; the client must call <see cref="Restart"/> before changes take
/// effect.
/// </summary>
[Route("api/admin/plugins")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PluginsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Host lifetime used to trigger a graceful restart after a plugin change.</summary>
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    /// <param name="lifetime">Host lifetime used to trigger a graceful restart after a plugin change.</param>
    public PluginsController(IMessageBus bus, IHostApplicationLifetime lifetime)
    {
        _bus = bus;
        _lifetime = lifetime;
    }

    /// <summary>Every plugin installed on disk, with its load status.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the plugins; 401 without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PluginListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<PluginListItemDto>>>(new ListPluginsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Installs a plugin from an uploaded ZIP archive.</summary>
    /// <remarks>
    /// <see cref="RequestSizeLimitAttribute"/> caps the upload itself; the unpacked size and entry
    /// count are capped by the <c>Plugins</c> settings and answered with
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c>.
    /// </remarks>
    /// <param name="file">The uploaded ZIP file, absent when the form carried none. <c>IFormFile</c>
    /// cannot be a command member, so its stream is passed to the command; a missing file reaches
    /// the validator as a null stream and is refused there.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: true</c> on success; 400 with <c>VALIDATION_ERROR</c> (no file),
    /// <c>PLUGIN_MANIFEST_INVALID</c>, <c>PLUGIN_SDK_VERSION_UNSUPPORTED</c>,
    /// <c>PLUGIN_ID_INVALID</c>, <c>PLUGIN_ARCHIVE_UNSAFE</c>, <c>PLUGIN_HOST_ASSEMBLY_REFUSED</c> or
    /// <c>PLUGIN_NOT_TRUSTED</c>, or one of <c>PLUGIN_UNSIGNED</c>, <c>PLUGIN_SIGNATURE_MALFORMED</c>,
    /// <c>PLUGIN_SIGNATURE_KEY_UNKNOWN</c> and <c>PLUGIN_SIGNATURE_INVALID</c> when the host enforces signatures and the
    /// package fails that check while "allow unsigned plugins" is off; 401 without a session; 403 for a caller who is not an
    /// administrator; 409 with <c>PLUGIN_BUILT_IN</c> when the id belongs to a plugin shipped with
    /// the build; 413 when the upload is over the request limit or with
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("install")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PluginActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> InstallAsync(IFormFile? file, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream();
        var outcome = await _bus.InvokeAsync<Result<PluginActionResultDto>>(new InstallPluginCommand(content), ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes an uploaded plugin by deleting its files from disk.</summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: true</c> on success; 400 with <c>VALIDATION_ERROR</c> or
    /// <c>PLUGIN_ID_INVALID</c>; 401 without a session; 403 for a caller who is not an administrator;
    /// 404 with <c>PLUGIN_NOT_FOUND</c>; 409 with <c>PLUGIN_BUILT_IN</c> for a plugin shipped with
    /// the build; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(PluginActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginActionResultDto>>(new RemovePluginCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Triggers a graceful application restart so newly installed or removed plugins take effect.
    /// The process manager (systemd, Docker restart policy) brings the process back up. Not a use
    /// case of its own: there is no state to load or save, only the host's own shutdown.
    /// </summary>
    /// <returns>
    /// 202 with no body; 401 without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public IActionResult Restart()
    {
        _lifetime.StopApplication();
        return AcceptedResult(Result.Success());
    }
}
