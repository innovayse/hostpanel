using System.Globalization;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The part of a plugin call the client and the admin area share: matching the request to a declared route of an
/// enabled plugin, then, once the area's own handler has authorised the caller, reading the body within the cap,
/// auditing an unsafe call, calling the plugin and turning its reply into the host's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two steps, so authorisation sits between them.</b> <see cref="FindAsync"/> answers only "is there such a route
/// on an enabled plugin" and builds nothing of the plugin; <see cref="RunAsync"/> is called only after the area's
/// handler has decided the caller may call it. A disabled plugin, an unknown one and an undeclared route all end at the
/// first step with the same <c>PLUGIN_ROUTE_NOT_FOUND</c>, whoever asks.
/// </para>
/// <para>
/// <b>Plugin problem codes are namespaced.</b> A problem the plugin returns becomes the standard error body with its
/// own status and the sentence it gave, but its code is written <c>plugin.{pluginId}.{code}</c>. Host codes are
/// <c>SCREAMING_SNAKE</c> with no dot, so the prefix means a plugin can never answer with a host code, such as
/// <c>CLIENT_PERMISSION_DENIED</c>, and a frontend that branches on a host code is never fooled by one. A code that is
/// not 1 to 64 letters, digits, <c>_</c>, <c>.</c> or <c>-</c> is a plugin fault (<c>PLUGIN_FAILED</c>).
/// </para>
/// <para>
/// <b>A plugin that throws</b> is answered <c>PLUGIN_FAILED</c> (502) with no message and no stack; the exception is logged
/// with the plugin id. Only the caller's own cancellation propagates.
/// </para>
/// </remarks>
public sealed class PluginCallRunner
{
    /// <summary>The longest sentence of a plugin problem the host passes on; a longer one is cut.</summary>
    public const int DetailMaxLength = 500;

    /// <summary>The first part of every code a plugin's problem is answered with.</summary>
    public const string CodePrefix = "plugin";

    /// <summary>The longest activity log description the table stores.</summary>
    private const int AuditMaxLength = 1000;

    /// <summary>The size of one read of the request body.</summary>
    private const int ReadChunkBytes = 8192;

    /// <summary>
    /// A plugin's own problem code: 1 to 64 letters, digits, underscore, dot or hyphen, starting with a letter or digit.
    /// Anchored with <c>\A</c> and <c>\z</c>, never <c>$</c>, which also matches before a trailing newline.
    /// </summary>
    private static readonly Regex ProblemCodePattern = new(
        @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Finds the plugin and checks that it is switched on.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Builds the host facts and the store bound to the call.</summary>
    private readonly IPluginCallServices _services;

    /// <summary>Stages the audit line of an unsafe call.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line before the plugin runs.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>The body cap.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Writes the sentence a problem gets when the plugin gave none.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Structured logger; records a plugin that failed, with its id.</summary>
    private readonly ILogger<PluginCallRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginCallRunner"/> class.</summary>
    /// <param name="plugins">Finds the plugin and checks that it is switched on.</param>
    /// <param name="services">Builds the host facts and the store bound to the call.</param>
    /// <param name="activityLogger">Stages the audit line of an unsafe call.</param>
    /// <param name="db">Saves the audit line before the plugin runs.</param>
    /// <param name="options">The body cap.</param>
    /// <param name="messages">Writes the sentence a problem gets when the plugin gave none.</param>
    /// <param name="logger">Structured logger.</param>
    public PluginCallRunner(
        IPluginEndpointResolver plugins,
        IPluginCallServices services,
        IActivityLogger activityLogger,
        IHostpanelDbContext db,
        IOptions<PluginApiOptions> options,
        IErrorMessageResolver messages,
        ILogger<PluginCallRunner> logger)
    {
        _plugins = plugins;
        _services = services;
        _activityLogger = activityLogger;
        _db = db;
        _options = options;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>Matches a request to a declared route of an enabled plugin. Nothing of the plugin runs.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="area">The panel the request came through; only its routes are looked at.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The target, or <see langword="null"/> when the plugin is unknown, is not an app plugin, is switched off or not
    /// configured, or does not declare that method and path in <paramref name="area"/>.
    /// </returns>
    public async Task<PluginTarget?> FindAsync(string pluginId, PluginArea area, PluginHttpInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var manifest = _plugins.Find(pluginId);
        if (manifest is null)
        {
            return null;
        }

        var routes = area == PluginArea.Admin ? manifest.Api.Admin : manifest.Api.Client;
        var match = PluginRouteMatcher.Match(routes, input.Method, input.Path);
        if (match is null)
        {
            return null;
        }

        var build = await _plugins.PrepareAsync(manifest.Id, ct);
        return build is null ? null : new PluginTarget(manifest, area, match, build);
    }

    /// <summary>Calls the plugin for an already authorised caller and turns its reply into the host's.</summary>
    /// <param name="target">The matched route, from <see cref="FindAsync"/>.</param>
    /// <param name="caller">Who is calling, with the plugin's opaque references.</param>
    /// <param name="clientId">The host id of the client the call acts for; <see langword="null"/> on an admin call.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>PLUGIN_STREAMING_NOT_SUPPORTED</c> for a streaming route or a streamed reply;
    /// <c>PLUGIN_REQUEST_TOO_LARGE</c> when the body is over the cap; <c>PLUGIN_FAILED</c> when the plugin threw or broke
    /// its contract.
    /// </returns>
    public async Task<Result<PluginReplyDto>> RunAsync(
        PluginTarget target, PluginCaller caller, int? clientId, PluginHttpInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(input);

        var route = target.Match.Route;

        // Streaming is #641. Refused before the plugin is called, so a route that cannot be answered has no side effect.
        if (route.Streaming)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_STREAMING_NOT_SUPPORTED);
        }

        var read = await ReadBodyAsync(input, ct);
        if (read is not { } body)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        }

        // Recorded before the plugin runs: the line says the call was made, so it exists even if the plugin then
        // fails or the process stops. The caller (id, name, address) is stamped by the logger; the body is never logged.
        if (route.Method != PluginRouteMethod.Get)
        {
            await _activityLogger.RecordAsync(new ActivityLogEntry(clientId, Describe(target)), ct);
            await _db.SaveChangesAsync(ct);
        }

        var pluginId = target.Manifest.Id;
        try
        {
            var endpoints = target.Build();
            var services = _services.Create(new PluginCallBinding(pluginId, clientId, caller.ClientRef));
            var call = new PluginCall
            {
                Area = target.Area,
                Method = route.Method,
                Route = route.Path,
                Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                Caller = caller,
                Host = services.Host,
                Store = services.Store,
                RouteValues = target.Match.Values,
                Query = input.Query,
                Body = body,
                ContentType = body.Length == 0 ? null : input.ContentType,
            };

            var reply = await endpoints.HandleAsync(call, ct);
            return Map(pluginId, reply);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // Nothing of the exception reaches the caller; the log is where an operator reads it.
            _logger.LogError(
                ex, "Plugin {PluginId} failed on {Area} {Method} {Route}.", pluginId, target.Area, route.Method, route.Path);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }
    }

    /// <summary>Turns what the plugin answered into the host's reply.</summary>
    /// <param name="pluginId">The plugin that answered.</param>
    /// <param name="reply">The plugin's reply; a plugin that returns none broke its contract.</param>
    /// <returns>The reply, or the failure for a reply the host cannot forward.</returns>
    private Result<PluginReplyDto> Map(string pluginId, PluginReply? reply)
    {
        // The SDK factories refuse a status outside the contract, but the host does not rely on that: a reply built another
        // way (a reflected or subclassed type, a plugin compiled against a different SDK) is checked again here, because
        // this status is the one thing a plugin chooses that reaches the wire unchanged.
        if (reply is not null && !HasContractStatus(reply))
        {
            _logger.LogError("Plugin {PluginId} returned a {Kind} reply with status {Status}, which the contract does not allow.", pluginId, reply.Kind, reply.Status);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }

        switch (reply?.Kind)
        {
            case PluginReplyKind.Json:
                return Result<PluginReplyDto>.Success(new PluginReplyDto(reply.Status, reply.Body, null, null));
            case PluginReplyKind.NoContent:
                return Result<PluginReplyDto>.Success(new PluginReplyDto(reply.Status, null, null, null));
            case PluginReplyKind.Problem:
                return MapProblem(pluginId, reply);
            case PluginReplyKind.Stream:
                // Never enumerated: an iterator that was not started has run nothing and holds nothing to release.
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_STREAMING_NOT_SUPPORTED);
            default:
                _logger.LogError("Plugin {PluginId} returned {Reply}, which is not a reply.", pluginId, reply?.Kind.ToString() ?? "no reply");
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }
    }

    /// <summary>Whether a reply carries a status its kind may have: 200 to 203 with a body, 204 with none, 400 to 599 except 401 and 407 for a problem.</summary>
    /// <param name="reply">The plugin's reply.</param>
    /// <returns><see langword="true"/> when the status is one the contract allows for the reply's kind; a stream carries no status of its own.</returns>
    private static bool HasContractStatus(PluginReply reply)
    {
        return reply.Kind switch
        {
            PluginReplyKind.Json => reply.Status is >= 200 and <= 203 && reply.Body is not null,
            PluginReplyKind.NoContent => reply.Status == 204,
            PluginReplyKind.Problem => reply.Status is >= 400 and <= 599 and not (401 or 407),
            _ => true,
        };
    }

    /// <summary>Turns a plugin's problem into the standard error body's parts, with the code namespaced.</summary>
    /// <param name="pluginId">The plugin that answered.</param>
    /// <param name="reply">The plugin's problem reply.</param>
    /// <returns>The reply, or <c>PLUGIN_FAILED</c> when the problem's code is malformed.</returns>
    private Result<PluginReplyDto> MapProblem(string pluginId, PluginReply reply)
    {
        if (reply.Code is null || !ProblemCodePattern.IsMatch(reply.Code))
        {
            _logger.LogError("Plugin {PluginId} returned a problem whose code is not a valid code.", pluginId);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }

        var detail = string.IsNullOrWhiteSpace(reply.Detail)
            ? _messages.Resolve(HostpanelErrorCodes.PLUGIN_REPORTED_PROBLEM)
            : Truncate(reply.Detail.Trim(), DetailMaxLength);

        return Result<PluginReplyDto>.Success(
            new PluginReplyDto(reply.Status, null, $"{CodePrefix}.{pluginId}.{reply.Code}", detail));
    }

    /// <summary>Reads the request body, within the cap.</summary>
    /// <param name="input">The request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The body bytes (a slice of the one buffer read), empty when there is none; <see langword="null"/> when the request declares more than the cap or
    /// sends more than it, in which case no more than the cap and one chunk was read.
    /// </returns>
    private async Task<ReadOnlyMemory<byte>?> ReadBodyAsync(PluginHttpInput input, CancellationToken ct)
    {
        var cap = _options.Value.MaxBodyBytes;
        if (input.ContentLength > cap)
        {
            return null;
        }

        // The one copy of the body: a declared length sizes the buffer once (it is within the cap, checked above), and
        // the plugin is handed a slice of that same buffer, never a second array made with ToArray.
        using var buffer = input.ContentLength is { } declared ? new MemoryStream((int)declared) : new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await input.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > cap)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
    }

    /// <summary>The audit line of a call: the plugin, the area and the declared route, never the body or the concrete path.</summary>
    /// <param name="target">The matched route.</param>
    /// <returns>The description, cut to the width the activity log stores.</returns>
    private static string Describe(PluginTarget target)
    {
        var route = target.Match.Route;
        var description = $"Called Plugin Route - Plugin ID: {target.Manifest.Id} - Area: {target.Area} "
            + $"- Route: {route.Method.ToString().ToUpperInvariant()} {route.Path}";

        return Truncate(description, AuditMaxLength);
    }

    /// <summary>Cuts a text to a length.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The longest length to keep.</param>
    /// <returns>The text, or its first <paramref name="length"/> characters.</returns>
    private static string Truncate(string text, int length)
    {
        return text.Length <= length ? text : text[..length];
    }
}
