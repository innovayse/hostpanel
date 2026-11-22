using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/*
 * The websites and databases half of the plugin host API (#1294), an Application service for the same reason
 * PluginSshAccessService is: the host API is an Infrastructure adapter and an adapter never dispatches a handler.
 *
 * Every operation passes the shared PluginHostingGate in its fixed order, cheapest and least revealing first, and nothing
 * irreversible happens before all of it passed: (1) a client-area call whose plugin's manifest declares the matching
 * hostAccess kind and whose caller holds ViewModifyPasswords; (2) the service is the bound client's; (3) its product is
 * bound to the plugin; (4) the shape of the request; (5) the service has a usable hosting account on a server that can
 * set sites up; (6) the plugin's own records decide whether the call is already done, belongs to another plugin
 * (Conflict) or would pass the per-service limit; (7) the hosting-resources budget of the plugin and client, which fails
 * closed; (8) the plugin is still switched on; and only then the record is saved as Pending and the server is called.
 * A foreign service and a missing one answer NotFound alike. A removal asks (1), (2) and the plugin's own record, and is
 * allowed when the product is no longer bound: taking something away never depends on still being allowed to grant it.
 *
 * Idempotency lives in the records, not in the server: a repeat of an Active website with the same specification, or of an
 * Active database, answers from the record with no call to the server. The record is saved before the server is called, so
 * a crash or a timeout leaves a record that says what may exist, and a retry resumes it. The count against the limit and
 * the insert are serialised per service by the same save that moves ClientService.HostingResourceRevision, so two calls
 * cannot both pass it.
 *
 * A database password is returned once, from the server's answer to the call that created it. It is never stored, never put
 * in a log template or an activity line, and never part of an exception message.
 */

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Reads what a hosting service can do, and sets up and removes the websites and databases a plugin holds on a hosting
/// service of the client its call is bound to.
/// </summary>
public sealed class PluginHostingService
{
    /// <summary>How many times an add is counted and saved again after losing the service row to another add.</summary>
    private const int MaxSaveAttempts = 3;

    /// <summary>The longest pause before a retry, in milliseconds; the pause is drawn between zero and this.</summary>
    private const int MaxRetryJitterMilliseconds = 50;

    /// <summary>The gate every host access passes: declared access, ownership, operator binding and availability.</summary>
    private readonly PluginHostingGate _gate;

    /// <summary>Resolves the service's server, agent settings and account.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Creates the provider that sets sites up on the service's server.</summary>
    private readonly IHostingResourceProviderFactory _providers;

    /// <summary>The budget of hosting changes per plugin and client, taken before a server is called.</summary>
    private readonly IPluginHostAccessLimiter _limiter;

    /// <summary>Stages the activity log line in the same save as the record.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Data context the records and activity lines are saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<PluginHostingService> _logger;

    /// <summary>Initializes the service.</summary>
    /// <param name="gate">The gate every host access passes: declared access, ownership, operator binding and availability.</param>
    /// <param name="resolver">Resolves the service's server, agent settings and account.</param>
    /// <param name="providers">Creates the provider that sets sites up on the service's server.</param>
    /// <param name="limiter">The budget of hosting changes per plugin and client, taken before a server is called.</param>
    /// <param name="activity">Stages the activity log line in the same save as the record.</param>
    /// <param name="context">Data context the records and activity lines are saved through.</param>
    /// <param name="logger">Records a provider failure without putting its detail on the wire.</param>
    public PluginHostingService(
        PluginHostingGate gate,
        ISshServiceContextResolver resolver,
        IHostingResourceProviderFactory providers,
        IPluginHostAccessLimiter limiter,
        IActivityLogger activity,
        IHostpanelDbContext context,
        ILogger<PluginHostingService> logger)
    {
        _gate = gate;
        _resolver = resolver;
        _providers = providers;
        _limiter = limiter;
        _activity = activity;
        _context = context;
        _logger = logger;
    }

    /// <summary>Reads what the service's hosting account can do for the plugin of <paramref name="binding"/>.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the plugin asks about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The capabilities (every kind the manifest does not declare reads as false), or <c>NotAllowed</c>, <c>NotFound</c>
    /// or <c>ServerUnavailable</c>. A service that cannot take sites answers all false rather than a refusal.
    /// </returns>
    public async Task<PluginHostOutcome<HostedCapabilities>> GetCapabilitiesAsync(PluginCallBinding binding, int serviceId, CancellationToken ct)
    {
        var ssh = _gate.Declares(binding, PluginHostAccess.Ssh);
        var websites = _gate.Declares(binding, PluginHostAccess.Websites);
        var databases = _gate.Declares(binding, PluginHostAccess.Databases);
        if (!ssh && !websites && !databases)
        {
            return PluginHostOutcome<HostedCapabilities>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginHostOutcome<HostedCapabilities>.Refused(PluginHostAccessFailure.NotFound);
        }

        if (!await _gate.IsBoundAsync(binding, context.Service.ProductId, ct))
        {
            return PluginHostOutcome<HostedCapabilities>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var available = context.State is SshAccessState.Available;
        if (!available || !(websites || databases) || context.Target is not { } target)
        {
            return PluginHostOutcome<HostedCapabilities>.Granted(new HostedCapabilities(ssh && available, false, false, []));
        }

        if (_providers.CreateFor(target) is not { } provider)
        {
            return PluginHostOutcome<HostedCapabilities>.Granted(new HostedCapabilities(ssh, false, false, []));
        }

        // Asking opens a connection to the server, so it is counted like a change.
        if (!await TryTakeBudgetAsync(binding, context, ct))
        {
            return PluginHostOutcome<HostedCapabilities>.Refused(PluginHostAccessFailure.ServerUnavailable);
        }

        try
        {
            var reported = await provider.GetCapabilitiesAsync(ct);
            return PluginHostOutcome<HostedCapabilities>.Granted(new HostedCapabilities(
                ssh,
                websites && reported.Websites,
                databases && reported.Databases,
                websites && reported.Websites ? reported.PhpVersions : []));
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "Reading the hosting capabilities of server {ServerId} failed: {Failure}", target.ServerId, ex.Failure);
            return PluginHostOutcome<HostedCapabilities>.Refused(PluginHostAccessFailure.ServerUnavailable);
        }
    }

    /// <summary>Makes sure a website exists on <paramref name="serviceId"/> as <paramref name="spec"/> says.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the plugin asks about.</param>
    /// <param name="spec">The website to set up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The website, or <c>NotAllowed</c>, <c>NotFound</c>, <c>InvalidRequest</c>, <c>Unavailable</c>, <c>Conflict</c>
    /// (the host name is another plugin's, or the server holds a site of that name the host did not create),
    /// <c>LimitReached</c> or <c>ServerUnavailable</c>.
    /// </returns>
    public async Task<PluginHostOutcome<HostedWebsite>> EnsureWebsiteAsync(
        PluginCallBinding binding, int serviceId, HostedWebsiteSpec spec, CancellationToken ct)
    {
        if (!_gate.Declares(binding, PluginHostAccess.Websites))
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.NotFound);
        }

        if (!await _gate.IsBoundAsync(binding, context.Service.ProductId, ct))
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var hostname = spec.Hostname.Trim().ToLowerInvariant();
        var documentRoot = spec.DocumentRoot.Trim();
        var php = spec.PhpVersion?.Trim();
        if (!HostingSpecRules.IsHostname(hostname)
            || !HostingSpecRules.IsHostnameOfDomain(hostname, context.Service.Domain)
            || !HostingSpecRules.IsDocumentRoot(documentRoot)
            || (php is not null && !HostingSpecRules.IsPhpVersion(php))
            || (spec.DocumentRootMayBeLink && php is null))
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.InvalidRequest);
        }

        if (UsableProvider(context) is not { } usable)
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.Unavailable);
        }

        var (provider, account) = usable;
        var existing = await FindLiveWebsiteAsync(context.Service.Id, hostname, ct);
        if (existing is not null
            && existing.IsIssuedTo(binding.PluginId)
            && existing.State is HostingResourceState.Active
            && existing.IsBoundTo(account.ServerId, account.Username)
            && existing.Matches(documentRoot, php, spec.DocumentRootMayBeLink))
        {
            return PluginHostOutcome<HostedWebsite>.Granted(Describe(existing));
        }

        if (existing is not null && ResumeFailure(existing, account, binding.PluginId) is { } unusable)
        {
            return PluginHostOutcome<HostedWebsite>.Refused(unusable);
        }

        if (!await TryTakeBudgetAsync(binding, context, ct))
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.ServerUnavailable);
        }

        // The gate passed at the start of the call; the operator may have switched the plugin off since. Asked again
        // right before a record is saved and a server is called.
        if (!await _gate.IsStillAvailableAsync(binding, ct))
        {
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var staged = await StageAsync(
            context,
            account,
            binding.PluginId,
            () => FindLiveWebsiteAsync(context.Service.Id, hostname, ct),
            () => _context.ServiceWebsites.CountAsync(
                w => w.ClientServiceId == context.Service.Id && w.State != HostingResourceState.Removed, ct),
            HostingResourceLimits.MaxWebsitesPerService,
            service => ServiceWebsite.Create(
                service.Id, account.ServerId, account.Username, binding.PluginId, hostname, documentRoot, php, spec.DocumentRootMayBeLink),
            website => _context.ServiceWebsites.Add(website),
            ct);
        if (staged.Failure is { } refused)
        {
            return PluginHostOutcome<HostedWebsite>.Refused(refused);
        }

        var website = staged.Resource!;
        if (!staged.IsNew && website.State is HostingResourceState.Active && website.Matches(documentRoot, php, spec.DocumentRootMayBeLink))
        {
            // Another call of the same plugin finished the same website while this one waited.
            return PluginHostOutcome<HostedWebsite>.Granted(Describe(website));
        }

        try
        {
            await provider.EnsureWebsiteAsync(
                new WebsiteOrder(account, website.WebsiteId, hostname, documentRoot, php, spec.DocumentRootMayBeLink), ct);
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "Website {WebsiteId} on server {ServerId} failed: {Failure}", website.WebsiteId, account.ServerId, ex.Failure);
            if (staged.IsNew && !ex.IsOutcomeUnknown)
            {
                // A definite refusal: the website is not on the server, so the record would only describe nothing.
                _context.ServiceWebsites.Remove(website);
                await _context.SaveChangesAsync(CancellationToken.None);
            }

            return PluginHostOutcome<HostedWebsite>.Refused(FailureFor(ex.Failure));
        }

        if (website.State is HostingResourceState.Pending)
        {
            website.MarkActive();
        }

        if (!website.Matches(documentRoot, php, spec.DocumentRootMayBeLink))
        {
            // The server applied the specification asked for now, which a resumed or an Active record may not hold yet.
            website.Respecify(documentRoot, php, spec.DocumentRootMayBeLink);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Plugin {binding.PluginId} set up website {hostname} on service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);

        // A removal of the plugin that committed during the call may already have run its pass, which could not see this
        // website. Asked once more after the save: a plugin that is gone has the website taken back here.
        if (staged.IsNew && !await _gate.IsStillAvailableAsync(binding, ct))
        {
            await RemoveRecordedAsync(
                website,
                "website",
                website.WebsiteId,
                account.ServerId,
                () => provider.RemoveWebsiteAsync(new WebsiteRemoval(account, website.WebsiteId, hostname), ct));
            await _context.SaveChangesAsync(CancellationToken.None);
            return PluginHostOutcome<HostedWebsite>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        return PluginHostOutcome<HostedWebsite>.Granted(Describe(website));
    }

    /// <summary>Removes a website the plugin of <paramref name="binding"/> set up on <paramref name="serviceId"/>.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the website is on.</param>
    /// <param name="websiteId">The website, as the answer to its setup named it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> once the website is gone (also when it already was), otherwise <c>NotAllowed</c>,
    /// <c>NotFound</c> (also for another plugin's website), <c>Unavailable</c> or <c>ServerUnavailable</c> (the record
    /// then says the removal failed).
    /// </returns>
    public Task<PluginHostAccessFailure?> RemoveWebsiteAsync(
        PluginCallBinding binding, int serviceId, Guid websiteId, CancellationToken ct)
    {
        return RemoveHeldAsync(
            binding,
            serviceId,
            PluginHostAccess.Websites,
            context => _context.ServiceWebsites.FirstOrDefaultAsync(
                w => w.ClientServiceId == context.Service.Id && w.WebsiteId == websiteId && w.IssuedToPluginId == binding.PluginId, ct),
            new HeldResourceKind<ServiceWebsite>(
                "website",
                website => website.WebsiteId,
                website => website.Hostname,
                (provider, account, website) => provider.RemoveWebsiteAsync(new WebsiteRemoval(account, website.WebsiteId, website.Hostname), ct)),
            ct);
    }

    /// <summary>Makes sure a database exists on <paramref name="serviceId"/> for <paramref name="label"/>.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the plugin asks about.</param>
    /// <param name="label">What the database is for; 1 to 15 characters, a lower case letter then letters or digits.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The database, with the password only when this call created it, or <c>NotAllowed</c>, <c>NotFound</c>,
    /// <c>InvalidRequest</c>, <c>Unavailable</c>, <c>Conflict</c> (the label is another plugin's), <c>LimitReached</c> or
    /// <c>ServerUnavailable</c>.
    /// </returns>
    public async Task<PluginHostOutcome<HostedDatabase>> EnsureDatabaseAsync(
        PluginCallBinding binding, int serviceId, string label, CancellationToken ct)
    {
        if (!_gate.Declares(binding, PluginHostAccess.Databases))
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.NotFound);
        }

        if (!await _gate.IsBoundAsync(binding, context.Service.ProductId, ct))
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var trimmed = label.Trim();
        if (!HostingSpecRules.IsDatabaseLabel(trimmed))
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.InvalidRequest);
        }

        if (UsableProvider(context) is not { } usable)
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.Unavailable);
        }

        var (provider, account) = usable;
        var existing = await FindLiveDatabaseAsync(context.Service.Id, trimmed, binding.PluginId, ct);
        if (existing is not null
            && existing.IsIssuedTo(binding.PluginId)
            && existing.State is HostingResourceState.Active
            && existing.IsBoundTo(account.ServerId, account.Username))
        {
            // The password was handed out once, when the database was created, and the host keeps none.
            return PluginHostOutcome<HostedDatabase>.Granted(Describe(existing, null));
        }

        if (existing is not null && ResumeFailure(existing, account, binding.PluginId) is { } unusable)
        {
            return PluginHostOutcome<HostedDatabase>.Refused(unusable);
        }

        if (!await TryTakeBudgetAsync(binding, context, ct))
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.ServerUnavailable);
        }

        if (!await _gate.IsStillAvailableAsync(binding, ct))
        {
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        var staged = await StageAsync(
            context,
            account,
            binding.PluginId,
            () => FindLiveDatabaseAsync(context.Service.Id, trimmed, binding.PluginId, ct),
            () => _context.ServiceDatabases.CountAsync(
                d => d.ClientServiceId == context.Service.Id && d.State != HostingResourceState.Removed, ct),
            HostingResourceLimits.MaxDatabasesPerService,
            service => ServiceDatabase.Create(service.Id, account.ServerId, account.Username, binding.PluginId, trimmed),
            database => _context.ServiceDatabases.Add(database),
            ct);
        if (staged.Failure is { } refused)
        {
            return PluginHostOutcome<HostedDatabase>.Refused(refused);
        }

        var database = staged.Resource!;
        if (!staged.IsNew && database.State is HostingResourceState.Active)
        {
            // Another call of the same plugin finished the same database while this one waited.
            return PluginHostOutcome<HostedDatabase>.Granted(Describe(database, null));
        }

        ProvisionedDatabase provisioned;
        try
        {
            provisioned = await provider.EnsureDatabaseAsync(new DatabaseOrder(account, database.DatabaseId, trimmed), ct);
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "Database {DatabaseId} on server {ServerId} failed: {Failure}", database.DatabaseId, account.ServerId, ex.Failure);
            if (staged.IsNew && !ex.IsOutcomeUnknown)
            {
                _context.ServiceDatabases.Remove(database);
                await _context.SaveChangesAsync(CancellationToken.None);
            }

            return PluginHostOutcome<HostedDatabase>.Refused(FailureFor(ex.Failure));
        }

        database.MarkProvisioned(provisioned.Name, provisioned.Username);
        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Plugin {binding.PluginId} set up database {database.Name} on service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);

        if (staged.IsNew && !await _gate.IsStillAvailableAsync(binding, ct))
        {
            await RemoveRecordedAsync(
                database,
                "database",
                database.DatabaseId,
                account.ServerId,
                () => provider.RemoveDatabaseAsync(
                    new DatabaseRemoval(account, database.DatabaseId, database.Label, database.Name, database.Username), ct));
            await _context.SaveChangesAsync(CancellationToken.None);
            return PluginHostOutcome<HostedDatabase>.Refused(PluginHostAccessFailure.NotAllowed);
        }

        return PluginHostOutcome<HostedDatabase>.Granted(Describe(database, provisioned.Password));
    }

    /// <summary>Removes a database the plugin of <paramref name="binding"/> created on <paramref name="serviceId"/>.</summary>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the database is on.</param>
    /// <param name="databaseId">The database, as the answer to its setup named it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> once the database is gone (also when it already was), otherwise <c>NotAllowed</c>,
    /// <c>NotFound</c> (also for another plugin's database), <c>Unavailable</c> or <c>ServerUnavailable</c> (the record
    /// then says the removal failed).
    /// </returns>
    public Task<PluginHostAccessFailure?> RemoveDatabaseAsync(
        PluginCallBinding binding, int serviceId, Guid databaseId, CancellationToken ct)
    {
        return RemoveHeldAsync(
            binding,
            serviceId,
            PluginHostAccess.Databases,
            context => _context.ServiceDatabases.FirstOrDefaultAsync(
                d => d.ClientServiceId == context.Service.Id && d.DatabaseId == databaseId && d.IssuedToPluginId == binding.PluginId, ct),
            new HeldResourceKind<ServiceDatabase>(
                "database",
                database => database.DatabaseId,
                database => database.Name,
                (provider, account, database) => provider.RemoveDatabaseAsync(
                    new DatabaseRemoval(account, database.DatabaseId, database.Label, database.Name, database.Username), ct)),
            ct);
    }

    /// <summary>Resolves the service's SSH context when it belongs to the binding's client, through the gate's ownership rule.</summary>
    /// <param name="binding">The call; its client is set (checked by the gate's <c>Declares</c>).</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The context, or <see langword="null"/> for a missing service and another client's alike.</returns>
    private Task<SshServiceContext?> ResolveOwnedAsync(PluginCallBinding binding, int serviceId, CancellationToken ct)
    {
        return _gate.ResolveOwnedAsync(binding, serviceId, _resolver.ResolveAsync, context => context.Service.ClientId, ct);
    }

    /// <summary>The provider and account of a service whose server can set sites up.</summary>
    /// <param name="context">The service's context.</param>
    /// <returns>The pair, or <see langword="null"/> when the service has no usable hosting account or its server cannot set sites up.</returns>
    private (IHostingResourceProvider Provider, SshAccount Account)? UsableProvider(SshServiceContext context)
    {
        if (context.State is not SshAccessState.Available || context.Account is not { } account || context.Target is not { } target)
        {
            return null;
        }

        return _providers.CreateFor(target) is { } provider ? (provider, account) : null;
    }

    /// <summary>Takes one change from the plugin's hosting budget for the service's client; fails closed.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="context">The service's context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the call may go on.</returns>
    private Task<bool> TryTakeBudgetAsync(PluginCallBinding binding, SshServiceContext context, CancellationToken ct)
    {
        return _limiter.TryAcquireAsync(
            PluginHostAccessKey.ForClient(binding.PluginId, context.Service.ClientId, RateLimitPolicies.HostingResources), ct);
    }

    /// <summary>The service's website of that host name that is not removed, of any plugin.</summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="hostname">The host name, lower case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked website, or <see langword="null"/> when none is live.</returns>
    private Task<ServiceWebsite?> FindLiveWebsiteAsync(int serviceId, string hostname, CancellationToken ct)
    {
        return _context.ServiceWebsites.FirstOrDefaultAsync(
            w => w.ClientServiceId == serviceId && w.Hostname == hostname && w.State != HostingResourceState.Removed, ct);
    }

    /// <summary>The service's database of that label that is not removed: the plugin's own first, else another plugin's.</summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="label">The label.</param>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked database, or <see langword="null"/> when none is live.</returns>
    private async Task<ServiceDatabase?> FindLiveDatabaseAsync(int serviceId, string label, string pluginId, CancellationToken ct)
    {
        var live = await _context.ServiceDatabases
            .Where(d => d.ClientServiceId == serviceId && d.Label == label && d.State != HostingResourceState.Removed)
            .ToListAsync(ct);
        return live.FirstOrDefault(d => d.IssuedToPluginId == pluginId) ?? live.FirstOrDefault();
    }

    /// <summary>
    /// Decides what the request means against the service's live resources of the kind and, for a new one, records it as
    /// pending in the save that moves the service's resource revision, so two adds cannot both pass the limit.
    /// </summary>
    /// <typeparam name="TResource">The kind of resource: a website or a database.</typeparam>
    /// <param name="context">The service's context.</param>
    /// <param name="account">The service's current account.</param>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="findLive">Reads the service's live resource of the requested name, or <see langword="null"/>.</param>
    /// <param name="countLive">Counts the service's resources of the kind that are not removed.</param>
    /// <param name="limit">The most resources of the kind a service may hold.</param>
    /// <param name="create">Builds the pending resource for the service row read for this attempt.</param>
    /// <param name="add">Starts tracking the new resource.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The resource to call the server for and whether it is new, or the refusal: <c>Conflict</c> (another plugin's name),
    /// <c>Unavailable</c> (the resource sits on another server or its removal failed), <c>LimitReached</c> or
    /// <c>ServerUnavailable</c> (the save kept losing to other adds).
    /// </returns>
    private async Task<(TResource? Resource, bool IsNew, PluginHostAccessFailure? Failure)> StageAsync<TResource>(
        SshServiceContext context,
        SshAccount account,
        string pluginId,
        Func<Task<TResource?>> findLive,
        Func<Task<int>> countLive,
        int limit,
        Func<ClientService, TResource> create,
        Action<TResource> add,
        CancellationToken ct)
        where TResource : HostingResource
    {
        for (var attempt = 1; ; attempt++)
        {
            var existing = await findLive();
            if (existing is not null)
            {
                return ResumeFailure(existing, account, pluginId) is { } failure
                    ? (null, false, failure)
                    : (existing, false, null);
            }

            // The service row is read before the count: its xmin token then covers the count, so a change saved
            // after this read (and before the count) makes this save conflict instead of passing the limit unseen.
            var service = await _context.ClientServices.FirstAsync(s => s.Id == context.Service.Id, ct);
            if (await countLive() >= limit)
            {
                return (null, false, PluginHostAccessFailure.LimitReached);
            }

            var resource = create(service);
            service.RecordHostingResourceChanged();
            add(resource);
            try
            {
                await _context.SaveChangesAsync(ct);
                return (resource, true, null);
            }
            catch (ConcurrencyConflictException)
            {
                // Another add of this service saved first (the service row's token moved, or the same name won the
                // unique index): drop only what this attempt staged and decide again from a fresh read.
                _context.Detach(resource);
                _context.Detach(service);
                if (attempt >= MaxSaveAttempts)
                {
                    return (null, false, PluginHostAccessFailure.ServerUnavailable);
                }

                // Not a secret, so the shared generator is fine: it only spreads two racing retries apart.
                await Task.Delay(Random.Shared.Next(MaxRetryJitterMilliseconds + 1), ct);
            }
        }
    }

    /// <summary>Why a live resource of the requested name cannot be resumed by this call, or <see langword="null"/> when it can.</summary>
    /// <param name="existing">The live resource.</param>
    /// <param name="account">The service's current account.</param>
    /// <param name="pluginId">The plugin asking.</param>
    /// <returns><c>Conflict</c> for another plugin's, <c>Unavailable</c> for one on another server or whose removal failed.</returns>
    private static PluginHostAccessFailure? ResumeFailure(HostingResource existing, SshAccount account, string pluginId)
    {
        if (!existing.IsIssuedTo(pluginId))
        {
            return PluginHostAccessFailure.Conflict;
        }

        return !existing.IsBoundTo(account.ServerId, account.Username) || existing.State is HostingResourceState.RemoveFailed
            ? PluginHostAccessFailure.Unavailable
            : null;
    }

    /// <summary>
    /// Removes a resource the plugin holds on a service: the shared flow of a website and a database removal, which asks the
    /// gate, finds the plugin's own record, takes a change from the budget, removes it on the server and saves the outcome.
    /// </summary>
    /// <typeparam name="TResource">The kind of resource: a website or a database.</typeparam>
    /// <param name="binding">The call: plugin, area and the client it acts for.</param>
    /// <param name="serviceId">The service the resource is on.</param>
    /// <param name="access">The host access kind the plugin's manifest must declare.</param>
    /// <param name="find">Reads the plugin's own record of the resource on the service, or <see langword="null"/>.</param>
    /// <param name="kind">What differs between the kinds: the name, the ids and the server call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="null"/> once the resource is gone (also when it already was), otherwise <c>NotAllowed</c>,
    /// <c>NotFound</c>, <c>Unavailable</c> or <c>ServerUnavailable</c> (the record then says the removal failed).
    /// </returns>
    private async Task<PluginHostAccessFailure?> RemoveHeldAsync<TResource>(
        PluginCallBinding binding,
        int serviceId,
        string access,
        Func<SshServiceContext, Task<TResource?>> find,
        HeldResourceKind<TResource> kind,
        CancellationToken ct)
        where TResource : HostingResource
    {
        if (!_gate.Declares(binding, access))
        {
            return PluginHostAccessFailure.NotAllowed;
        }

        var context = await ResolveOwnedAsync(binding, serviceId, ct);
        if (context is null)
        {
            return PluginHostAccessFailure.NotFound;
        }

        var resource = await find(context);
        if (resource is null)
        {
            return PluginHostAccessFailure.NotFound;
        }

        if (resource.State is HostingResourceState.Removed)
        {
            return null;
        }

        if (UsableProvider(context) is not { } usable || !resource.IsBoundTo(usable.Account.ServerId, usable.Account.Username))
        {
            return PluginHostAccessFailure.Unavailable;
        }

        if (!await TryTakeBudgetAsync(binding, context, ct))
        {
            return PluginHostAccessFailure.ServerUnavailable;
        }

        var removed = await RemoveRecordedAsync(
            resource,
            kind.Name,
            kind.IdOf(resource),
            usable.Account.ServerId,
            () => kind.RemoveOnServer(usable.Provider, usable.Account, resource));
        await RecordRemovalAsync(binding, context, $"{kind.Name} {kind.LabelOf(resource)}", removed, ct);
        return removed ? null : PluginHostAccessFailure.ServerUnavailable;
    }

    /// <summary>Removes a resource on the server and records the outcome on it. Does not save.</summary>
    /// <typeparam name="TResource">The kind of resource: a website or a database.</typeparam>
    /// <param name="resource">The tracked resource.</param>
    /// <param name="kind">The kind's name, for the log, for example <c>website</c>.</param>
    /// <param name="resourceId">The id the plugin knows the resource by.</param>
    /// <param name="serverId">The server the resource is on.</param>
    /// <param name="removeOnServer">Asks the server's provider to take the resource down.</param>
    /// <returns><see langword="true"/> once the server confirmed it is gone; otherwise the resource records the failed removal.</returns>
    private async Task<bool> RemoveRecordedAsync<TResource>(
        TResource resource, string kind, Guid resourceId, int serverId, Func<Task> removeOnServer)
        where TResource : HostingResource
    {
        try
        {
            await removeOnServer();
            resource.MarkRemoved();
            return true;
        }
        catch (SshProviderException ex)
        {
            _logger.LogWarning(ex, "Removing {Kind} {ResourceId} on server {ServerId} failed: {Failure}", kind, resourceId, serverId, ex.Failure);
            resource.MarkRemoveFailed();
            return false;
        }
    }

    /// <summary>Stages the activity line of a removal and saves it with the record's new state.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="context">The service's context.</param>
    /// <param name="what">What was removed, for example <c>website example.com</c>; never a secret.</param>
    /// <param name="removed">Whether the server confirmed the removal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the state and the line are saved.</returns>
    private async Task RecordRemovalAsync(PluginCallBinding binding, SshServiceContext context, string what, bool removed, CancellationToken ct)
    {
        var line = removed
            ? $"Plugin {binding.PluginId} removed {what} from service #{context.Service.Id}"
            : $"Plugin {binding.PluginId} could not remove {what} from service #{context.Service.Id}";
        await _activity.RecordAsync(new ActivityLogEntry(context.Service.ClientId, line), ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>The SDK's reason for an agent refusal.</summary>
    /// <param name="failure">What the provider reported.</param>
    /// <returns>The reason the plugin is told.</returns>
    private static PluginHostAccessFailure FailureFor(SshProviderFailure failure)
    {
        return failure switch
        {
            SshProviderFailure.WebsiteConflict or SshProviderFailure.SiteNotOwned => PluginHostAccessFailure.Conflict,
            SshProviderFailure.PhpVersionUnsupported
                or SshProviderFailure.InvalidRequest
                or SshProviderFailure.UnsafePath => PluginHostAccessFailure.InvalidRequest,
            SshProviderFailure.DatabaseLimit => PluginHostAccessFailure.LimitReached,
            SshProviderFailure.NotSupported
                or SshProviderFailure.AccountSuspended
                or SshProviderFailure.AccountNotEligible
                or SshProviderFailure.AccountNotFound
                or SshProviderFailure.InvalidAccount => PluginHostAccessFailure.Unavailable,
            _ => PluginHostAccessFailure.ServerUnavailable,
        };
    }

    /// <summary>The host's answer for a website record.</summary>
    /// <param name="website">The record.</param>
    /// <returns>The website as the plugin is told of it.</returns>
    private static HostedWebsite Describe(ServiceWebsite website)
    {
        return new HostedWebsite(
            website.WebsiteId,
            website.Hostname,
            website.DocumentRoot,
            HostingSpecRules.DocumentRootPath(website.AccountUsername, website.DocumentRoot),
            website.PhpVersion);
    }

    /// <summary>The host's answer for a database record.</summary>
    /// <param name="database">The record.</param>
    /// <param name="password">The password when the database was just created, otherwise <see langword="null"/>.</param>
    /// <returns>The database as the plugin is told of it.</returns>
    private static HostedDatabase Describe(ServiceDatabase database, string? password)
    {
        return new HostedDatabase(
            database.DatabaseId,
            database.Name,
            database.Username,
            HostingSpecRules.DatabaseHost,
            HostingSpecRules.DatabasePort,
            password);
    }
}
