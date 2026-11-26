using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reports how the plugin changes a restart applied ended, once the host is up and the database can be reached: the loader
/// decides them while it starts, before any request or database context exists. Each change gets a line in the activity log
/// and a <see cref="PluginChangeEndedEvent"/>, which tells the admins by mail. A change an admin's own request applied in
/// place is answered to that admin and never reaches this service.
/// </summary>
/// <remarks>
/// A change is marked as reported only after its line is saved and its event is published, so a start that cannot reach the
/// database leaves it for the next one. The event is the same on every try, so the mail it causes is sent once. A failure here
/// is logged and never stops the host.
/// </remarks>
public sealed class PluginChangeReporter : IHostedLifecycleService
{
    /// <summary>The most characters of an id, version or load error written to an audit line; the values come from an uploaded plugin.</summary>
    private const int MaxAuditValueLength = 200;

    /// <summary>The records of the changes and whether each was written.</summary>
    private readonly PluginChangeStore _changes;

    /// <summary>Opens the scope the audit line is staged and saved in.</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Records a change that could not be written.</summary>
    private readonly ILogger<PluginChangeReporter> _logger;

    /// <summary>Initializes the reporter.</summary>
    /// <param name="changes">The records of the changes and whether each was written.</param>
    /// <param name="scopeFactory">Opens the scope the audit line is staged and saved in.</param>
    /// <param name="logger">Records a change that could not be written.</param>
    public PluginChangeReporter(PluginChangeStore changes, IServiceScopeFactory scopeFactory, ILogger<PluginChangeReporter> logger)
    {
        _changes = changes;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>No-op: the changes are written once the host is up.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op: the changes are written once the host is up.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Writes every change that has no activity log line yet, each failure kept to its change.</summary>
    /// <param name="cancellationToken">Cancelled when the host gives up starting.</param>
    /// <returns>A task that completes when every change was written or left for the next start.</returns>
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        foreach (var change in _changes.Unreported())
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IActivityLogger>()
                    .RecordAsync(new ActivityLogEntry(null, Describe(change)), cancellationToken);
                await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(cancellationToken);
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(Ended(change));
                _changes.MarkReported(change.PluginId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not write the change of plugin {Id} to the activity log, it is tried again at the next start", change.PluginId);
            }
        }
    }

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>No-op.</summary>
    /// <param name="cancellationToken">Cancelled when the host's shutdown grace period ends.</param>
    /// <returns>A completed task.</returns>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Words the audit line of a change a restart applied.</summary>
    /// <param name="change">The change and how it ended.</param>
    /// <returns>The line: who is not named, because the host itself applied it; the earlier line names the admin.</returns>
    private static string Describe(PluginChangeDto change)
    {
        var line = $"Plugin ID: {Clean(change.PluginId)} - Version: {Clean(change.FromVersion)} to {Clean(change.ToVersion)}";
        return change.Result == PluginChangeResult.Applied
            ? $"Plugin Change Applied By Restart - {line}"
            : $"Plugin Change Failed By Restart - {line} - Restored Version: {Clean(change.RestoredVersion)} - Reason: {Clean(change.Error)}";
    }

    /// <summary>Builds the event that tells the admins how a change a restart applied ended.</summary>
    /// <param name="change">The change and how it ended.</param>
    /// <returns>The event; its values are cleaned like the audit line, and it is the same for every try, so its mail is sent once.</returns>
    private static PluginChangeEndedEvent Ended(PluginChangeDto change)
    {
        return new PluginChangeEndedEvent(
            Clean(change.PluginId),
            Clean(change.FromVersion).NullIfBlank(),
            Clean(change.ToVersion),
            change.Result == PluginChangeResult.Applied,
            Clean(change.Error).NullIfBlank(),
            Clean(change.RestoredVersion).NullIfBlank(),
            change.At);
    }

    /// <summary>Makes a value taken from an uploaded plugin safe for an audit line and a mail.</summary>
    /// <param name="value">The text from the plugin or from its load error.</param>
    /// <returns>The cleaned text; empty for <see langword="null"/>.</returns>
    private static string Clean(string? value)
    {
        return UntrustedText.Clean(value, MaxAuditValueLength);
    }
}
