using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.ClearRegistryPluginLicence;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallPlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallRegistryPlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.PurgePluginData;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RetryRegistryInstance;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SaveRegistryPluginLicence;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.CheckPluginUpdates;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetPluginIcon;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryInstance;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryPlugin;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.InspectPluginPackage;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPlugins;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListRegistryPlugins;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Every plugin installed on disk, with its status: loaded (<c>status: "Loaded"</c>, with <c>stagedVersion</c> when
    /// an upgrade waits for the restart), refused for a broken manifest rule (<c>"Refused"</c>, with <c>refusedRule</c>
    /// and <c>refusedDetail</c>), or removed with its data still kept (<c>"Removed"</c>).
    /// </summary>
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

    /// <summary>
    /// The icon a loaded plugin ships (<c>iconUrl</c> on the plugin row points here), served from memory with
    /// <c>X-Content-Type-Options: nosniff</c>, a restrictive Content-Security-Policy and an entity tag.
    /// </summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the image (<c>image/svg+xml</c> or <c>image/png</c>); 304 when the caller's <c>If-None-Match</c> holds
    /// the current tag; 400 with <c>VALIDATION_ERROR</c> for an id that is not a plugin id; 401 without a session; 403
    /// for a caller who is not an administrator; 404 with <c>PLUGIN_ICON_NOT_FOUND</c> alike for an unknown plugin and
    /// for a plugin without an icon; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id}/icon")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "image/svg+xml", "image/png")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIconAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginIconDto>>(new GetPluginIconQuery(id), ct);
        return HandleResult(outcome, icon => InlineImage(icon.Content, icon.ContentType, icon.ETag));
    }

    /// <summary>
    /// Reads what an uploaded plugin ZIP asks for before it is installed: the host access and capabilities it declares, the
    /// release notes of the version and the Hostpanel versions it supports, with whether this server is inside that range.
    /// Nothing is unpacked or installed; the admin panel shows the answer and asks for confirmation before the install.
    /// </summary>
    /// <param name="file">The uploaded ZIP file, absent when the form carried none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the preview; 400 with <c>VALIDATION_ERROR</c> (no file) or <c>PLUGIN_MANIFEST_INVALID</c> when the archive
    /// or its <c>plugin.json</c> cannot be read; 401 without a session; 403 for a caller who is not an administrator; 413
    /// when the upload is over the request limit; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("inspect")]
    [RequestSizeLimit(52_428_800)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PluginPackagePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> InspectAsync(IFormFile? file, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream();
        var outcome = await _bus.InvokeAsync<Result<PluginPackagePreviewDto>>(new InspectPluginPackageQuery(content), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Installs a plugin from an uploaded ZIP archive. When a plugin of the same id is installed and the uploaded
    /// version differs, the upload is an upgrade: it passes the same checks, is staged, and replaces the installed
    /// plugin at the next restart, which then runs the plugin's migration hook once. An older version is refused unless
    /// <c>force</c> is true.
    /// </summary>
    /// <remarks>
    /// <see cref="RequestSizeLimitAttribute"/> caps the upload itself; the unpacked size and entry
    /// count are capped by the <c>Plugins</c> settings and answered with
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c>.
    /// </remarks>
    /// <param name="file">The uploaded ZIP file, absent when the form carried none. <c>IFormFile</c>
    /// cannot be a command member, so its stream is passed to the command; a missing file reaches
    /// the validator as a null stream and is refused there.</param>
    /// <param name="force">Query flag: <see langword="true"/> accepts a version older than the installed one.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: true</c> on success (and <c>stagedVersion</c> when the upload is an upgrade or a forced
    /// downgrade waiting for the restart); 400 with <c>VALIDATION_ERROR</c> (no file),
    /// <c>PLUGIN_MANIFEST_INVALID</c>, <c>PLUGIN_SDK_VERSION_UNSUPPORTED</c>,
    /// <c>PLUGIN_ID_INVALID</c>, <c>PLUGIN_ARCHIVE_UNSAFE</c>, <c>PLUGIN_ARCHIVE_UNWRITABLE</c>, <c>PLUGIN_HOST_ASSEMBLY_REFUSED</c> or
    /// <c>PLUGIN_NOT_TRUSTED</c>, or one of <c>PLUGIN_UNSIGNED</c>, <c>PLUGIN_SIGNATURE_MALFORMED</c>,
    /// <c>PLUGIN_SIGNATURE_KEY_UNKNOWN</c> and <c>PLUGIN_SIGNATURE_INVALID</c> when the host enforces signatures and the
    /// package fails that check while "allow unsigned plugins" is off; 401 without a session; 403 for a caller who is not an
    /// administrator; 409 with <c>PLUGIN_BUILT_IN</c> when the id belongs to a plugin shipped with
    /// the build, <c>PLUGIN_DOWNGRADE_REFUSED</c> when the upload is older than the installed version and <c>force</c>
    /// is not set, or <c>PLUGIN_HOST_VERSION_OUT_OF_RANGE</c> when the running Hostpanel version is below the package's
    /// <c>minHostVersion</c> or above its <c>maxHostVersion</c> (a bound it leaves out is no limit); 413 when the upload is over the request limit or with
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
    public async Task<IActionResult> InstallAsync(IFormFile? file, [FromQuery] bool force, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream();
        var outcome = await _bus.InvokeAsync<Result<PluginActionResultDto>>(new InstallPluginCommand(content, force), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// One page of the plugin registry, each plugin marked with whether it is installed here, at which version and whether
    /// a newer one is published. The registry is a remote catalogue the operator configures; it is off until then.
    /// </summary>
    /// <param name="query">
    /// The filters bound from the query string: <c>q</c> (free text), <c>category</c> (<c>Payment</c>, <c>Provisioning</c>,
    /// <c>Registrar</c>, <c>App</c> or <c>Other</c>), <c>page</c> and <c>pageSize</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>items</c>, <c>total</c>, <c>page</c> and <c>pageSize</c>; 400 with <c>VALIDATION_ERROR</c> for a search
    /// over 100 characters or an undefined category; 401 without a session; 403 for a caller who is not an administrator;
    /// 409 with <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off; 429 when the rate limit is hit; 502 with
    /// <c>PLUGIN_REGISTRY_UNAVAILABLE</c> or <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c> when the registry cannot be used; 504
    /// with <c>PLUGIN_REGISTRY_TIMED_OUT</c> when it does not answer in time.
    /// </returns>
    [HttpGet("registry")]
    [ProducesResponseType(typeof(RegistryPluginPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> ListRegistryAsync([FromQuery] ListRegistryPluginsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RegistryPluginPageDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// The installed, uploaded plugins for which the registry publishes a newer version, in the same shape as the
    /// registry browser rows (<c>installed</c> and <c>updateAvailable</c> are true).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the plugins (an empty array when nothing is outdated); 401 without a session; 403 for a caller who is not
    /// an administrator; 409 with <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off; 429 when the rate limit is
    /// hit; 502 with <c>PLUGIN_REGISTRY_UNAVAILABLE</c> or <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c>; 504 with
    /// <c>PLUGIN_REGISTRY_TIMED_OUT</c>.
    /// </returns>
    [HttpGet("registry/updates")]
    [ProducesResponseType(typeof(IReadOnlyList<RegistryPluginDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> CheckRegistryUpdatesAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<RegistryPluginDto>>>(new CheckPluginUpdatesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Where this installation stands with the plugin registry: not registered yet, pending, verified, revoked or failed (with the
    /// code and the sentence of the last failure). The identity is created and confirmed automatically; this only reports it.
    /// Nothing secret is returned.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the state; 401 without a session; 403 for a caller who is not an administrator; 429 when the rate limit is hit.</returns>
    [HttpGet("registry-instance")]
    [ProducesResponseType(typeof(RegistryInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRegistryInstanceAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RegistryInstanceDto>>(new GetRegistryInstanceQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Registers this installation with the plugin registry again at once, ignoring the back-off, and registers anew after a
    /// revocation. A run that fails is still an answer: the returned state says what went wrong.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the state after the run; 401 without a session; 403 for a caller who is not an administrator or in a demo
    /// installation; 409 with <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("registry-instance/retry")]
    [ProducesResponseType(typeof(RegistryInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryRegistryInstanceAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RegistryInstanceDto>>(new RetryRegistryInstanceCommand(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// The details page of one registry plugin: the registry's page and versions, set beside what is installed on this
    /// server. Each version says whether it runs on this Hostpanel version (<c>compatible</c>). The licence key stored for
    /// the plugin is sent to the registry; the key itself is never returned, only <c>hasLicenceKey</c>.
    /// </summary>
    /// <param name="id">Plugin identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the details; 400 with <c>VALIDATION_ERROR</c> for an id that is not a plugin id; 401 without a session; 403
    /// for a caller who is not an administrator; 404 with <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> for a plugin the registry
    /// does not list; 409 with <c>PLUGIN_REGISTRY_DISABLED</c> while the registry is off; 422 with
    /// <c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c> or <c>PLUGIN_REGISTRY_LICENCE_INVALID</c>; 429 when the rate limit is hit; 502 with
    /// <c>PLUGIN_REGISTRY_UNAVAILABLE</c> or <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c>; 504 with <c>PLUGIN_REGISTRY_TIMED_OUT</c>.
    /// </returns>
    [HttpGet("registry/{id}")]
    [ProducesResponseType(typeof(RegistryPluginDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GetRegistryAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RegistryPluginDetailsDto>>(new GetRegistryPluginQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Stores the licence key of a paid or private registry plugin, encrypted, replacing any key stored before. The key
    /// is write only: no endpoint returns it. The change is audited without the key.
    /// </summary>
    /// <param name="id">Plugin identifier.</param>
    /// <param name="command">The body: the licence <c>key</c> (1 to 512 characters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once stored; 400 with <c>VALIDATION_ERROR</c> for an id of the wrong shape or an empty or over long key; 401 without
    /// a session; 403 for a caller who is not an administrator; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("registry/{id}/licence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveRegistryLicenceAsync(string id, [FromBody] SaveRegistryPluginLicenceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { PluginId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes the licence key stored for a registry plugin. Deleting a key that is not stored answers the same.</summary>
    /// <param name="id">Plugin identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once no key is stored; 400 with <c>VALIDATION_ERROR</c> for an id of the wrong shape; 401 without a session; 403 for
    /// a caller who is not an administrator; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("registry/{id}/licence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClearRegistryLicenceAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new ClearRegistryPluginLicenceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Installs one version of a plugin from the registry, or stages it as an upgrade when the plugin is installed. The
    /// package is downloaded from the registry's own host, checked against the checksum the registry published, held to
    /// the <c>plugins.registry_trust</c> setting and installed like an upload with its signature always enforced.
    /// </summary>
    /// <param name="id">Plugin identifier.</param>
    /// <param name="command">
    /// The body: the <c>version</c> to install and, optionally, <c>allowDowngrade</c> (default <see langword="false"/>), which
    /// accepts a version older than the installed one. The licence key stored for the plugin is sent to the registry.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: true</c> (and <c>stagedVersion</c> for an upgrade or a downgrade); 400 with
    /// <c>VALIDATION_ERROR</c> for an id or version of the wrong shape, or with a package rule (<c>PLUGIN_MANIFEST_INVALID</c>,
    /// <c>PLUGIN_UNSIGNED</c>, <c>PLUGIN_SIGNATURE_INVALID</c> and the other install refusals); 401 without a session; 403
    /// for a caller who is not an administrator or with <c>PLUGIN_REGISTRY_NOT_TRUSTED</c> when the trust setting declines
    /// the plugin; 404 with <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c>; 409 with <c>PLUGIN_REGISTRY_DISABLED</c>,
    /// <c>PLUGIN_BUILT_IN</c>, <c>PLUGIN_DOWNGRADE_REFUSED</c>, <c>PLUGIN_REGISTRY_HOST_INCOMPATIBLE</c>,
    /// <c>PLUGIN_HOST_VERSION_OUT_OF_RANGE</c>,
    /// <c>PLUGIN_REGISTRY_VERSION_YANKED</c> or <c>PLUGIN_REGISTRY_VERSION_COMPROMISED</c>; 413 with
    /// <c>PLUGIN_ARCHIVE_TOO_LARGE</c>; 422 with <c>PLUGIN_REGISTRY_LICENCE_REQUIRED</c> or <c>PLUGIN_REGISTRY_LICENCE_INVALID</c>;
    /// 429 when the rate limit is hit; 502 with <c>PLUGIN_REGISTRY_UNAVAILABLE</c>, <c>PLUGIN_REGISTRY_RESPONSE_INVALID</c>,
    /// <c>PLUGIN_REGISTRY_PACKAGE_REFUSED</c> or <c>PLUGIN_REGISTRY_CHECKSUM_MISMATCH</c>; 504 with
    /// <c>PLUGIN_REGISTRY_TIMED_OUT</c>.
    /// </returns>
    [HttpPost("registry/{id}/install")]
    [ProducesResponseType(typeof(PluginActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> InstallRegistryAsync(string id, [FromBody] InstallRegistryPluginCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginActionResultDto>>(command with { PluginId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Removes an uploaded plugin: deletes its files (and any upgrade staged for it) and switches it off. The data the
    /// plugin kept is not deleted; <see cref="PurgeDataAsync"/> does that. A valid, non-built-in id whose files are
    /// already gone is switched off the same way.
    /// </summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: true</c> when the files were removed, or <c>requiresRestart: false</c> when they were
    /// already gone; 400 with <c>VALIDATION_ERROR</c> or <c>PLUGIN_ID_INVALID</c>; 401 without a session; 403 for a caller
    /// who is not an administrator; 404 with <c>PLUGIN_NOT_FOUND</c> when no files are left and the plugin was never
    /// switched on here; 409 with <c>PLUGIN_BUILT_IN</c> for a plugin shipped with the build; 429 when the rate limit is
    /// hit.
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
    /// Deletes the data a removed plugin kept in the plugin store (and whatever else the host keeps for that plugin id).
    /// The purge is queued and audited; rows the plugin wrote after this request are kept.
    /// </summary>
    /// <param name="id">Plugin identifier whose kept data is deleted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>requiresRestart: false</c> once the purge is recorded and queued; 400 with <c>VALIDATION_ERROR</c>;
    /// 401 without a session; 403 for a caller who is not an administrator; 404 with <c>PLUGIN_NOT_FOUND</c> when the
    /// plugin keeps no data; 409 with <c>PLUGIN_STILL_INSTALLED</c> while its package is installed; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpDelete("{id}/data")]
    [ProducesResponseType(typeof(PluginActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurgeDataAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginActionResultDto>>(new PurgePluginDataCommand(id), ct);
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
