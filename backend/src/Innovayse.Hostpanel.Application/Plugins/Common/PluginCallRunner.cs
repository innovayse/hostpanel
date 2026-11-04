using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The part of a plugin call the client and the admin area share: once <see cref="PluginCallGate"/> has admitted the
/// caller to a declared route of an enabled plugin, reading the body within the cap, auditing an unsafe call, calling
/// the plugin and turning its reply into the host's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two steps, so authorisation sits before the plugin.</b> <see cref="PluginCallGate"/> answers "is there such a route
/// on an enabled plugin this caller may use" and builds nothing of the plugin; <see cref="RunAsync"/> is called only with
/// the target it admitted. A disabled plugin, an unknown one, an undeclared route and a route the caller may not use all
/// end at the gate with the same <c>PLUGIN_ROUTE_NOT_FOUND</c>, whoever asks.
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
/// with the plugin id. Only the caller's own cancellation propagates. The one exception is a throw after the host's own
/// store refused a write over its quota (<see cref="PluginStoreQuotaSignal"/>, host state the plugin cannot set): that call
/// is answered <c>PLUGIN_STORE_QUOTA_EXCEEDED</c> (402), whether the plugin let the store's refusal escape or threw on its
/// own after catching it.
/// </para>
/// <para>
/// <b>A time limit per call.</b> The token the plugin receives fires after <see cref="PluginApiOptions.CallTimeoutSeconds"/>
/// (every route today; a per route limit up to <see cref="PluginApiOptions.MaxCallTimeoutSeconds"/> waits for an SDK
/// field); the caller then gets <c>PLUGIN_TIMEOUT</c> (504) with no text of the plugin, and the id is logged. Anything the
/// plugin does after the token fired, a thrown exception or a reply it still returns, is a timeout. A plugin that ignores
/// the token is abandoned after <see cref="PluginApiOptions.AbandonWaitSeconds"/>; its work runs on, counted in
/// <see cref="PluginAbandonedCalls"/>, and while the plugin holds <see cref="PluginApiOptions.MaxAbandonedCallsPerPlugin"/> of
/// them, new calls to it are refused at once with <c>PLUGIN_TIMEOUT</c>. The limit covers the wait for the reply; on a stream it is the longest silence between
/// events (see <see cref="StreamIdleLimit"/>), never the total length of a stream that keeps sending. The audit line of an
/// unsafe call is written before the plugin runs and is not changed by a timeout.
/// </para>
/// <para>
/// <b>One binding per call.</b> The area's handler builds the <see cref="PluginCallBinding"/> once; the runner builds the
/// plugin's <see cref="PluginCaller"/> from it and hands the same binding to <see cref="IPluginCallServices"/>, so the
/// client reference the plugin sees is, by construction, the scope of its store, and the facts follow the same permissions.
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

    /// <summary>
    /// A plugin's own problem code: 1 to 64 letters, digits, underscore, dot or hyphen, starting with a letter or digit.
    /// Anchored with <c>\A</c> and <c>\z</c>, never <c>$</c>, which also matches before a trailing newline.
    /// </summary>
    private static readonly Regex ProblemCodePattern = new(
        @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Builds the host facts and the store bound to the call.</summary>
    private readonly IPluginCallServices _services;

    /// <summary>Stages the audit line of an unsafe call.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Saves the audit line before the plugin runs.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The body cap, the call limit and the abandon rules.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Counts the calls abandoned per plugin.</summary>
    private readonly PluginAbandonedCalls _abandoned;

    /// <summary>Writes the sentence a problem gets when the plugin gave none.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Structured logger; records a plugin that failed, with its id.</summary>
    private readonly ILogger<PluginCallRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginCallRunner"/> class.</summary>
    /// <param name="services">Builds the host facts and the store bound to the call.</param>
    /// <param name="activityLogger">Stages the audit line of an unsafe call.</param>
    /// <param name="context">Saves the audit line before the plugin runs.</param>
    /// <param name="options">The body cap, the call limit and the abandon rules.</param>
    /// <param name="abandoned">Counts the calls abandoned per plugin.</param>
    /// <param name="messages">Writes the sentence a problem gets when the plugin gave none.</param>
    /// <param name="logger">Structured logger.</param>
    public PluginCallRunner(
        IPluginCallServices services,
        IActivityLogger activityLogger,
        IHostpanelDbContext context,
        IOptions<PluginApiOptions> options,
        PluginAbandonedCalls abandoned,
        IErrorMessageResolver messages,
        ILogger<PluginCallRunner> logger)
    {
        _services = services;
        _activityLogger = activityLogger;
        _context = context;
        _options = options;
        _abandoned = abandoned;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>Calls the plugin for an already authorised caller and turns its reply into the host's.</summary>
    /// <param name="target">The matched route, from <see cref="PluginCallGate"/>.</param>
    /// <param name="binding">
    /// What the call is bound to, built once by the area's handler: the plugin, the client and its opaque reference, and the
    /// caller's permissions. The plugin's <see cref="PluginCaller"/>, the audit line, the host facts and the store all come
    /// from it.
    /// </param>
    /// <param name="userRef">The plugin's opaque reference of the signed-in user.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="recordActivity">
    /// Whether an unsafe call writes the audit line. <see langword="false"/> for a public callback: it has no signed-in caller to
    /// stamp, and a line per unauthenticated request would let anyone grow the log; the plugin records what it verified.
    /// </param>
    /// <returns>
    /// The plugin's reply, which on a streaming route may be a stream (not enumerated here: the response enumerates it
    /// with the caller's abort token, see <see cref="PluginEventStream"/>); <c>PLUGIN_REQUEST_TOO_LARGE</c> when the body is over the cap; <c>PLUGIN_FAILED</c> when the plugin threw or broke
    /// its contract; <c>PLUGIN_TIMEOUT</c> when it did not reply within the call time limit; <c>PLUGIN_STORE_QUOTA_EXCEEDED</c> when the plugin threw after the host's store refused one of its writes
    /// over the scope's quota.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="binding"/> names another plugin than <paramref name="target"/>.</exception>
    public async Task<Result<PluginReplyDto>> RunAsync(
        PluginTarget target,
        PluginCallBinding binding,
        string userRef,
        PluginHttpInput input,
        CancellationToken ct,
        bool recordActivity = true)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrEmpty(userRef);
        ArgumentNullException.ThrowIfNull(input);

        // A binding for another plugin would hand this plugin another plugin's references and store scope.
        if (!string.Equals(binding.PluginId, target.Manifest.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("The binding names another plugin than the matched route.", nameof(binding));
        }

        var route = target.Match.Route;

        var read = await PluginBodyReader.ReadAsync(input, _options.Value.MaxBodyBytes, ct);
        if (read is not { } body)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        }

        // Recorded before the plugin runs: the line says the call was made, so it exists even if the plugin then
        // fails or the process stops. The caller (id, name, address) is stamped by the logger; the body is never logged.
        if (recordActivity && route.Method != PluginRouteMethod.Get)
        {
            await _activityLogger.RecordAsync(new ActivityLogEntry(binding.ClientId, Describe(target)), ct);
            await _context.SaveChangesAsync(ct);
        }

        var pluginId = target.Manifest.Id;
        var caller = new PluginCaller
        {
            ClientRef = binding.ClientRef,
            UserRef = userRef,
            Permissions = HeldNames(binding.Permissions),
        };

        // Declared outside the try so the catch can read this call's quota signal.
        PluginCallServices? services = null;
        try
        {
            var endpoints = target.Build();
            services = _services.Create(binding);
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
                Headers = input.Headers ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            };

            var limit = _options.Value.CallTimeoutFor(null);
            var (reply, timedOut) = await CallWithinAsync(pluginId, endpoints, call, limit, ct);
            if (timedOut)
            {
                // Nothing of the plugin reaches the caller; the id is the operator's lead.
                _logger.LogWarning(
                    "Plugin {PluginId} did not answer {Area} {Method} {Route} within {Timeout}.", pluginId, target.Area, route.Method, route.Path, limit);
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_TIMEOUT);
            }

            return Map(pluginId, target.Area, route, reply, limit);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The signal is host state set only by the store when it refused a write, so a plugin cannot fake this answer.
            if (services is { Quota.Raised: true })
            {
                _logger.LogWarning("Plugin {PluginId} went over its store quota.", pluginId);
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_STORE_QUOTA_EXCEEDED);
            }

            // Nothing of the exception reaches the caller; the log is where an operator reads it.
            _logger.LogError(
                ex, "Plugin {PluginId} failed on {Area} {Method} {Route}.", pluginId, target.Area, route.Method, route.Path);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }
    }

    /// <summary>
    /// Calls the plugin with a token that fires after the limit. A plugin that honours the token ends by itself; one that
    /// ignores it is abandoned after <see cref="PluginApiOptions.AbandonWaitSeconds"/>, so the request, and the scope it holds,
    /// can end, and its work is counted until it ends. Once the token has fired, whatever the plugin then does, throw or
    /// return a reply, is a timeout.
    /// </summary>
    /// <param name="pluginId">The plugin, for the log.</param>
    /// <param name="endpoints">The plugin's endpoints.</param>
    /// <param name="call">The call to make.</param>
    /// <param name="limit">How long the plugin may take to reply.</param>
    /// <param name="ct">Cancelled when the caller leaves; it is never answered as a timeout.</param>
    /// <returns>The plugin's reply, or <c>timedOut</c> when the limit passed before it answered or the plugin is at its abandoned limit.</returns>
    private async Task<(PluginReply? Reply, bool TimedOut)> CallWithinAsync(
        string pluginId, IPluginEndpoints endpoints, PluginCall call, TimeSpan limit, CancellationToken ct)
    {
        var options = _options.Value;
        if (!_abandoned.HasRoom(pluginId, options.MaxAbandonedCallsPerPlugin))
        {
            _logger.LogWarning(
                "Plugin {PluginId} still has {Count} abandoned calls running; the call was refused.", pluginId, _abandoned.CountOf(pluginId));
            return (null, true);
        }

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(limit);

        var work = endpoints.HandleAsync(call, timer.Token);
        using var abandon = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var finished = await Task.WhenAny(work, Task.Delay(limit + options.AbandonWait, abandon.Token));
        if (finished != work)
        {
            ct.ThrowIfCancellationRequested();

            _abandoned.Track(pluginId, work);
            _logger.LogWarning("Plugin {PluginId} ignored its cancellation; the call was abandoned.", pluginId);
            return (null, true);
        }

        await abandon.CancelAsync();
        try
        {
            var reply = await work;

            // A plugin that caught the cancellation and answered anyway answered too late.
            return TimerFired(timer, ct) ? (null, true) : (reply, false);
        }
        catch (Exception) when (TimerFired(timer, ct))
        {
            return (null, true);
        }
    }

    /// <summary>Whether the call's own limit, not the caller leaving, has cancelled the plugin's token.</summary>
    /// <param name="timer">The source whose token the plugin received.</param>
    /// <param name="ct">The caller's token.</param>
    /// <returns><see langword="true"/> when the limit fired and the caller is still there.</returns>
    private static bool TimerFired(CancellationTokenSource timer, CancellationToken ct)
    {
        return timer.IsCancellationRequested && !ct.IsCancellationRequested;
    }

    /// <summary>The names of the single flags a caller holds, as the plugin lists them.</summary>
    /// <param name="held">The permissions the caller holds on the account; <see cref="ClientPermission.None"/> on an admin call.</param>
    /// <returns>The flag names; neither <c>None</c> nor the combined <c>All</c>.</returns>
    private static List<string> HeldNames(ClientPermission held)
    {
        return Enum.GetValues<ClientPermission>()
            .Where(flag => flag is not (ClientPermission.None or ClientPermission.All) && held.HasFlag(flag))
            .Select(flag => flag.ToString())
            .ToList();
    }

    /// <summary>Turns what the plugin answered into the host's reply.</summary>
    /// <param name="pluginId">The plugin that answered.</param>
    /// <param name="area">The area the call came through; only a callback may answer with a raw body.</param>
    /// <param name="route">The route that was called; only a route that declares <c>streaming</c> may answer with a stream.</param>
    /// <param name="reply">The plugin's reply; a plugin that returns none broke its contract.</param>
    /// <param name="limit">The call's time limit; on a stream it is the longest silence between events.</param>
    /// <returns>The reply, or the failure for a reply the host cannot forward.</returns>
    private Result<PluginReplyDto> Map(string pluginId, PluginArea area, PluginRoute route, PluginReply? reply, TimeSpan limit)
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
                return MapStream(pluginId, route, reply, limit);
            case PluginReplyKind.Raw:
                return MapRaw(pluginId, area, reply);
            default:
                _logger.LogError("Plugin {PluginId} returned {Reply}, which is not a reply.", pluginId, reply?.Kind.ToString() ?? "no reply");
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }
    }

    /// <summary>The media types a raw callback reply may carry; anything else, markup and script included, is refused.</summary>
    private static readonly HashSet<string> AllowedRawMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "application/json",
        "application/xml",
        "application/x-www-form-urlencoded",
    };

    /// <summary>
    /// Turns a plugin's raw reply into the host's. A raw body carries a media type the plugin chose, so it is accepted only
    /// on a callback, whose answer goes to an outside system; on a panel route it could serve markup from the API's own origin
    /// to a signed-in browser. The media type is parsed again here and must be one of <see cref="AllowedRawMediaTypes"/>
    /// (a gateway acknowledgement is plain text, JSON, XML or a form, never markup or script), and the body is held to the request body cap.
    /// </summary>
    /// <param name="pluginId">The plugin that answered.</param>
    /// <param name="area">The area the call came through.</param>
    /// <param name="reply">The plugin's raw reply.</param>
    /// <returns>The reply carrying the bytes and the media type, or <c>PLUGIN_FAILED</c> when the area, the media type or the size is not allowed.</returns>
    private Result<PluginReplyDto> MapRaw(string pluginId, PluginArea area, PluginReply reply)
    {
        if (area != PluginArea.Callback
            || string.IsNullOrWhiteSpace(reply.ContentType)
            || !MediaTypeHeaderValue.TryParse(reply.ContentType, out var mediaType)
            || mediaType.MediaType is null
            || !AllowedRawMediaTypes.Contains(mediaType.MediaType)
            || reply.RawBody.Length > _options.Value.MaxBodyBytes)
        {
            _logger.LogError("Plugin {PluginId} returned a raw reply that is not allowed here: not a callback, a media type outside the allowed list or a body over the cap.", pluginId);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }

        return Result<PluginReplyDto>.Success(new PluginReplyDto(reply.Status, null, null, null, null, reply.RawBody, reply.ContentType));
    }

    /// <summary>Turns a plugin's stream into the host's, when the route declared one.</summary>
    /// <param name="pluginId">The plugin that answered.</param>
    /// <param name="route">The route that was called.</param>
    /// <param name="reply">The plugin's stream reply.</param>
    /// <param name="limit">The longest silence between events before the stream is ended with <c>PLUGIN_TIMEOUT</c>.</param>
    /// <returns>
    /// The reply carrying the events and the heartbeat, or <c>PLUGIN_FAILED</c> when the route did not declare streaming or the
    /// plugin gave no events; the events are never enumerated in that case, so an iterator that was not started holds
    /// nothing to release.
    /// </returns>
    private Result<PluginReplyDto> MapStream(string pluginId, PluginRoute route, PluginReply reply, TimeSpan limit)
    {
        if (!route.Streaming || reply.Events is null)
        {
            _logger.LogError("Plugin {PluginId} streamed a reply from a route that does not declare streaming, or gave no events.", pluginId);
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_FAILED);
        }

        // Resolved now, while the request's language is known, not later inside the stream. The token is the placeholder of
        // an iterator parameter: the response enumerates with the request abort token, which replaces it.
        var failed = _messages.Resolve(HostpanelErrorCodes.PLUGIN_FAILED);
        var events = PluginEventStream.TranslateAsync(pluginId, reply.Events, failed, _logger, _options.Value.StreamEventMaxBytes, CancellationToken.None);
        var idle = new StreamIdleLimit(
            limit,
            PluginEventStream.TimeoutEvents(_messages.Resolve(HostpanelErrorCodes.PLUGIN_TIMEOUT)),
            () => _logger.LogWarning("Plugin {PluginId} stayed silent for {Timeout} on a stream; the stream was ended.", pluginId, limit));
        var stream = new StreamedReply(events, _options.Value.StreamHeartbeat, idle);

        return Result<PluginReplyDto>.Success(new PluginReplyDto(reply.Status, null, null, null, stream));
    }

    /// <summary>Whether a reply carries a status its kind may have: 200 to 203 with a body, 204 with none, 400 to 599 except 401 and 407 for a problem, 200 to 599 except 401 and 407 for a raw body.</summary>
    /// <param name="reply">The plugin's reply.</param>
    /// <returns><see langword="true"/> when the status is one the contract allows for the reply's kind; a stream carries no status of its own.</returns>
    private static bool HasContractStatus(PluginReply reply)
    {
        return reply.Kind switch
        {
            PluginReplyKind.Json => reply.Status is >= 200 and <= 203 && reply.Body is not null,
            PluginReplyKind.NoContent => reply.Status == 204,
            PluginReplyKind.Problem => reply.Status is >= 400 and <= 599 and not (401 or 407),
            PluginReplyKind.Raw => reply.Status is >= 200 and <= 599 and not (401 or 407),

            // A stream (checked against its route by MapStream) and a kind this host does not know carry no status rule here. An unknown
            // kind must not pass as valid: Map's default arm answers it PLUGIN_FAILED, so this method does not decide it.
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
