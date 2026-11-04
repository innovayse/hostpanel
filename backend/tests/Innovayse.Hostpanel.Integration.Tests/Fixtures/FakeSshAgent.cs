using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One server's key agent in memory, recording every call it receives.</summary>
public sealed class FakeSshAgent : ISshKeyProvider
{
    /// <summary>The server this agent runs on.</summary>
    private readonly int _serverId;

    /// <summary>Installed keys per account.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, string>> _accounts = new();

    /// <summary>Initialises the agent.</summary>
    /// <param name="serverId">The server this agent runs on.</param>
    public FakeSshAgent(int serverId)
    {
        _serverId = serverId;
    }

    /// <summary>Every call received, as "op:account".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>When set, every call throws with this failure.</summary>
    public SshProviderFailure? FailWith { get; set; }

    /// <summary>
    /// Runs while a key is being added, after it is on the account and before the call returns, so a test can make
    /// something happen in that window (a plugin switched off mid-issue); <see langword="null"/> for nothing.
    /// </summary>
    public Func<Task>? WhileAdding { get; set; }

    /// <summary>The binding this agent reports on ping; null like an install that predates bindings.</summary>
    public Guid? Binding { get; set; }

    /// <summary>Whether accounts have a login shell.</summary>
    public bool ShellEnabled { get; set; } = true;

    /// <summary>The keys installed for one account.</summary>
    /// <param name="username">The account.</param>
    /// <returns>Key ids to key lines.</returns>
    public IReadOnlyDictionary<Guid, string> KeysOf(string username) => Account(username);

    /// <inheritdoc/>
    public Task<SshAgentInfo> PingAsync(CancellationToken ct)
    {
        Record("ping", null);
        return Task.FromResult(new SshAgentInfo("1.1.0", "cwp", Binding));
    }

    /// <inheritdoc/>
    public Task<SshAccessStatus> GetAccessStatusAsync(SshAccount account, CancellationToken ct)
    {
        Record("status", account);
        return Task.FromResult(new SshAccessStatus(ShellEnabled, Account(account.Username).Count, 0));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<InstalledSshKey>> ListKeysAsync(SshAccount account, CancellationToken ct)
    {
        Record("list", account);
        IReadOnlyList<InstalledSshKey> keys = Account(account.Username).Keys.Select(k => new InstalledSshKey(k, "fp")).ToList();
        return Task.FromResult(keys);
    }

    /// <inheritdoc/>
    public async Task AddKeyAsync(SshAccount account, Guid keyId, string publicKey, CancellationToken ct)
    {
        Record("add", account);
        Account(account.Username)[keyId] = publicKey;
        if (WhileAdding is { } during)
        {
            await during();
        }
    }

    /// <inheritdoc/>
    public Task RevokeKeyAsync(SshAccount account, Guid keyId, CancellationToken ct)
    {
        Record("revoke", account);
        Account(account.Username).TryRemove(keyId, out _);
        return Task.CompletedTask;
    }

    /// <summary>Records a call, refusing another server's account as the real provider does.</summary>
    /// <param name="op">Operation.</param>
    /// <param name="account">The account, if any.</param>
    private void Record(string op, SshAccount? account)
    {
        if (account is not null && account.ServerId != _serverId)
        {
            throw new InvalidOperationException($"Agent on server {_serverId} got an account of server {account.ServerId}.");
        }

        Calls.Enqueue($"{op}:{account?.Username}");
        if (FailWith is not null)
        {
            throw new SshProviderException(FailWith.Value, "fake failure");
        }
    }

    /// <summary>The key set of one account, created on first use.</summary>
    /// <param name="username">The account.</param>
    /// <returns>The key set.</returns>
    private ConcurrentDictionary<Guid, string> Account(string username) => _accounts.GetOrAdd(username, _ => new());
}
