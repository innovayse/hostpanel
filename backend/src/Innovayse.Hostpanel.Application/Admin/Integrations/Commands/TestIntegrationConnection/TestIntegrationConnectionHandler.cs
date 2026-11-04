using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Commands.TestIntegrationConnection;

/// <summary>
/// Handles <see cref="TestIntegrationConnectionCommand"/> by checking whether all required fields
/// for the integration contain non-empty stored values, and, for integrations that support it,
/// running a live probe against the provider.
/// </summary>
public sealed class TestIntegrationConnectionHandler
{
    /// <summary>
    /// Plugin id / slug of the hosted payment gateway. The provider assembly already exposes this as
    /// the provider's plugin id constant, but Application cannot reference a provider
    /// assembly, so it is repeated here as a single source shared by both usages below.
    /// </summary>
    private const string HostedGatewaySlug = IntegrationSlugs.Inecobank;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Payment plugin resolver, used for live gateway probes.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>Invalidates the cached hosting-panel server-info results a test call would make stale.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>Live credential checks for built-in integrations, matched by slug.</summary>
    private readonly IEnumerable<IIntegrationConnectionProbe> _probes;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<TestIntegrationConnectionHandler> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>The loaded plugins, for a slug that names no built-in integration.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>Asks a plugin that implements <c>IPluginStatus</c> and caches its answer for the Integrations page.</summary>
    private readonly PluginConnectionReader _connections;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Payment plugin resolver, used for live gateway probes.</param>
    /// <param name="cache">Invalidates the cached hosting-panel server-info results a test call would make stale.</param>
    /// <param name="probes">Live credential checks for built-in integrations, matched by slug.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="plugins">The loaded plugins, for a slug that names no built-in integration.</param>
    /// <param name="connections">Asks a plugin that implements <c>IPluginStatus</c> and caches its answer for the Integrations page.</param>
    public TestIntegrationConnectionHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        IMemoryCache cache,
        IEnumerable<IIntegrationConnectionProbe> probes,
        ILogger<TestIntegrationConnectionHandler> logger,
        IClock clock,
        IPluginRegistry plugins,
        PluginConnectionReader connections)
    {
        _plugins = plugins;
        _connections = connections;
        _context = context;
        _pluginResolver = pluginResolver;
        _cache = cache;
        _probes = probes;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Checks whether all required credential fields for the given integration slug are stored and
    /// non-empty, then returns a result describing the outcome.
    /// </summary>
    /// <param name="command">Command containing the integration slug to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IntegrationTestResultDto"/> with <c>Success = true</c> when fully configured
    /// (or successfully probed), or <c>Success = false</c> listing what is missing or reporting the
    /// probe's outcome, or <c>INTEGRATION_NOT_FOUND</c> when the slug names neither a built-in
    /// integration nor a loaded plugin, or <c>INTEGRATION_NOT_TESTABLE</c> for a plugin without <c>IPluginStatus</c>.
    /// </returns>
    public async Task<Result<IntegrationTestResultDto>> HandleAsync(
        TestIntegrationConnectionCommand command, CancellationToken ct)
    {
        var testedAt = _clock.UtcNow;

        if (!IntegrationCatalogue.TryGetTestable(command.Slug, out var meta))
        {
            return await TestPluginAsync(command.Slug, ct);
        }

        var result = await RunTestAsync(command.Slug, meta.RequiredFields, testedAt, ct);

        // A tested hosting-panel credential invalidates the cached live server-info result
        // the read side serves for 5 minutes, whatever the probe's own outcome.
        if (command.Slug.Equals(IntegrationSlugs.Cwp, StringComparison.OrdinalIgnoreCase))
        {
            _cache.Remove(HostingPanelCacheKeys.Cwp);
        }

        return Result<IntegrationTestResultDto>.Success(result);
    }

    /// <summary>
    /// Tests a plugin integration by asking its <c>IPluginStatus</c> now (#855). The answer also replaces the cached
    /// state its card shows, so the page and the test never disagree.
    /// </summary>
    /// <param name="slug">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The outcome in the request's language; <c>INTEGRATION_NOT_FOUND</c> when no plugin with that id is loaded,
    /// <c>INTEGRATION_NOT_TESTABLE</c> when it does not implement <c>IPluginStatus</c>.
    /// </returns>
    private async Task<Result<IntegrationTestResultDto>> TestPluginAsync(string slug, CancellationToken ct)
    {
        var manifest = _plugins.GetLoadedManifests().FirstOrDefault(m => m.Id == slug);
        if (manifest is null)
        {
            return Result<IntegrationTestResultDto>.FailureCode(HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
        }

        var connection = await _connections.RefreshAsync(manifest, ct);
        if (connection is null)
        {
            return Result<IntegrationTestResultDto>.FailureCode(HostpanelErrorCodes.INTEGRATION_NOT_TESTABLE);
        }

        // Degraded still works, with reduced function, so it passes; the message says which.
        var works = connection.State is IntegrationConnectionState.Connected or IntegrationConnectionState.Degraded;
        return Result<IntegrationTestResultDto>.Success(
            new IntegrationTestResultDto(works, connection.Message, connection.CheckedAt));
    }

    /// <summary>Runs the field-completeness check and, where supported, the live probe.</summary>
    /// <param name="slug">The integration slug under test.</param>
    /// <param name="requiredFields">The fields that must be present for the slug to be configured.</param>
    /// <param name="testedAt">The moment the test was run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome to report to the caller.</returns>
    private async Task<IntegrationTestResultDto> RunTestAsync(
        string slug, IReadOnlyList<string> requiredFields, DateTimeOffset testedAt, CancellationToken ct)
    {
        // Integrations with no required fields (e.g. bank-transfer) are always "configured".
        if (requiredFields.Count == 0)
        {
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Connection validated -- all required fields are configured.",
                TestedAt: testedAt);
        }

        var settings = await IntegrationSettingsLookup.LoadAsync(_context, IntegrationSettingKeys.Prefix(slug), ct);

        var missing = requiredFields
            .Where(field => !settings.HasValue(slug, field))
            .ToList();

        if (missing.Count > 0)
        {
            var missingList = string.Join(", ", missing);
            return new IntegrationTestResultDto(
                Success: false,
                Message: $"Missing required fields: {missingList}.",
                TestedAt: testedAt);
        }

        // Live probe for hosted-gateway plugins: a status query with a bogus order id
        // distinguishes valid credentials ("unregistered orderId" -> Declined result)
        // from rejected ones (access-denied error -> exception).
        if (slug == HostedGatewaySlug)
        {
            return await ProbeHostedGatewayAsync(testedAt, ct);
        }

        // Built-in integrations with a live probe: without it a "Connection OK" only meant
        // that the fields were not empty.
        var probe = _probes.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (probe is not null)
        {
            return await RunProbeAsync(probe, testedAt, ct);
        }

        return new IntegrationTestResultDto(
            Success: true,
            Message: "Connection validated -- all required fields are configured.",
            TestedAt: testedAt);
    }

    /// <summary>Runs a built-in integration's live credential check.</summary>
    /// <param name="probe">The probe registered for the integration's slug.</param>
    /// <param name="testedAt">The moment the test was run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The probe's outcome, with no provider detail on the wire.</returns>
    private async Task<IntegrationTestResultDto> RunProbeAsync(
        IIntegrationConnectionProbe probe, DateTimeOffset testedAt, CancellationToken ct)
    {
        try
        {
            await probe.ProbeAsync(ct);
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Provider reachable and credentials accepted.",
                TestedAt: testedAt);
        }
        catch (OperationCanceledException)
        {
            // An admin-cancelled request is not a provider failure: let it propagate.
            throw;
        }
        catch (Exception ex)
        {
            // The provider's own message never reaches the caller (rules on upstream detail); it is
            // logged here for whoever has to chase the failure up.
            _logger.LogWarning(ex, "Connection test probe for {Slug} failed.", probe.Slug);
            return new IntegrationTestResultDto(
                Success: false,
                Message: "Connection test failed. See the server log for detail.",
                TestedAt: testedAt);
        }
    }

    /// <summary>Runs the hosted payment gateway's live probe.</summary>
    /// <param name="testedAt">The moment the test was run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The probe's outcome, with no gateway detail on the wire.</returns>
    private async Task<IntegrationTestResultDto> ProbeHostedGatewayAsync(DateTimeOffset testedAt, CancellationToken ct)
    {
        var plugin = await _pluginResolver.ResolveAsync(HostedGatewaySlug, ct);
        if (plugin is null)
        {
            return new IntegrationTestResultDto(
                Success: false,
                Message: "Integration is disabled or the plugin is not loaded.",
                TestedAt: testedAt);
        }

        try
        {
            await plugin.GetStatusAsync("connection-test-probe", ct);
            return new IntegrationTestResultDto(
                Success: true,
                Message: "Gateway reachable and credentials accepted.",
                TestedAt: testedAt);
        }
        catch (OperationCanceledException)
        {
            // An admin-cancelled request (e.g. navigating away mid-probe) is not a gateway
            // failure: let it propagate rather than reporting "test failed".
            throw;
        }
        catch (Exception ex)
        {
            // The gateway's own message never reaches the caller (rules on upstream detail); it is
            // logged here for whoever has to chase the failure up.
            _logger.LogWarning(ex, "Hosted gateway connection test probe failed.");
            return new IntegrationTestResultDto(
                Success: false,
                Message: "Gateway test failed. See the server log for detail.",
                TestedAt: testedAt);
        }
    }
}
