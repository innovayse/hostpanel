using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One server's website and database agent in memory, recording every call it receives and behaving like the real one:
/// every operation is idempotent, a database password comes back only on the call that created the database, and a
/// repeat answers the same names with no password.
/// </summary>
public sealed class FakeHostingAgent : IHostingResourceProvider
{
    /// <summary>The password every database this agent creates gets, so a test can look for it in the database and the logs.</summary>
    public const string DatabasePassword = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The server this agent runs on.</summary>
    private readonly int _serverId;

    /// <summary>Websites per account and host name, holding the order that set them up.</summary>
    private readonly ConcurrentDictionary<(string Account, string Hostname), WebsiteOrder> _websites = new();

    /// <summary>Database names per account and label.</summary>
    private readonly ConcurrentDictionary<(string Account, string Label), string> _databases = new();

    /// <summary>Initialises the agent.</summary>
    /// <param name="serverId">The server this agent runs on.</param>
    public FakeHostingAgent(int serverId)
    {
        _serverId = serverId;
    }

    /// <summary>Every call received, as "op:account:what".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>When set, every call throws with this failure.</summary>
    public SshProviderFailure? FailWith { get; set; }

    /// <summary>What the server reports it can do; every operation and PHP 8.2 and 8.3 unless a test changes it.</summary>
    public HostingCapabilities Capabilities { get; set; } = new(true, true, ["8.2", "8.3"]);

    /// <summary>
    /// Runs while a website or database is being set up, after it exists on the server and before the call returns, so a
    /// test can make something happen in that window (a plugin switched off mid-call); <see langword="null"/> for nothing.
    /// </summary>
    public Func<Task>? WhileEnsuring { get; set; }

    /// <summary>The website set up for a host name of an account, or <see langword="null"/> when there is none.</summary>
    /// <param name="account">The account.</param>
    /// <param name="hostname">The host name.</param>
    /// <returns>The order that set it up.</returns>
    public WebsiteOrder? WebsiteOf(string account, string hostname)
    {
        return _websites.TryGetValue((account, hostname), out var order) ? order : null;
    }

    /// <summary>Whether the account holds a database of that label.</summary>
    /// <param name="account">The account.</param>
    /// <param name="label">The label.</param>
    /// <returns><see langword="true"/> while the database exists.</returns>
    public bool HasDatabase(string account, string label)
    {
        return _databases.ContainsKey((account, label));
    }

    /// <summary>How many calls of an operation were received.</summary>
    /// <param name="op">The operation, for example <c>ensure-website</c>.</param>
    /// <returns>The count.</returns>
    public int CallsOf(string op)
    {
        return Calls.Count(call => call.StartsWith(op + ":", StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public Task<HostingCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        Record("ping", null, string.Empty);
        return Task.FromResult(Capabilities);
    }

    /// <inheritdoc/>
    public async Task EnsureWebsiteAsync(WebsiteOrder order, CancellationToken ct)
    {
        Record("ensure-website", order.Account, order.Hostname);
        _websites[(order.Account.Username, order.Hostname)] = order;
        if (WhileEnsuring is { } during)
        {
            await during();
        }
    }

    /// <inheritdoc/>
    public Task RemoveWebsiteAsync(WebsiteRemoval removal, CancellationToken ct)
    {
        Record("remove-website", removal.Account, removal.Hostname);
        _websites.TryRemove((removal.Account.Username, removal.Hostname), out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ProvisionedDatabase> EnsureDatabaseAsync(DatabaseOrder order, CancellationToken ct)
    {
        Record("ensure-database", order.Account, order.Label);
        var name = $"{order.Account.Username}_{order.Label}";
        var created = _databases.TryAdd((order.Account.Username, order.Label), name);
        if (WhileEnsuring is { } during)
        {
            await during();
        }

        return new ProvisionedDatabase(name, name, created ? DatabasePassword : null);
    }

    /// <inheritdoc/>
    public Task RemoveDatabaseAsync(DatabaseRemoval removal, CancellationToken ct)
    {
        Record("remove-database", removal.Account, removal.Label);
        _databases.TryRemove((removal.Account.Username, removal.Label), out _);
        return Task.CompletedTask;
    }

    /// <summary>Records a call, refusing another server's account as the real provider does.</summary>
    /// <param name="op">Operation.</param>
    /// <param name="account">The account, if any.</param>
    /// <param name="what">The host name or label the call is about.</param>
    private void Record(string op, SshAccount? account, string what)
    {
        if (account is not null && account.ServerId != _serverId)
        {
            throw new InvalidOperationException($"Agent on server {_serverId} got an account of server {account.ServerId}.");
        }

        Calls.Enqueue($"{op}:{account?.Username}:{what}");
        if (FailWith is not null)
        {
            throw new SshProviderException(FailWith.Value, "fake failure");
        }
    }
}
