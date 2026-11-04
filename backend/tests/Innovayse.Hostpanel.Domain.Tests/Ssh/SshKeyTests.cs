using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Ssh;

/// <summary>State and server-binding rules of <see cref="SshKey"/>.</summary>
public sealed class SshKeyTests
{
    /// <summary>Creates a key for account <c>web</c> on server 1 with a padded label.</summary>
    /// <returns>A fresh pending key.</returns>
    private static SshKey NewKey() => SshKey.Create(10, 1, "web", " laptop ", "ssh-ed25519 AAAA", "SHA256:x", null);

    /// <summary>A new key is pending, has a stable key id and a trimmed label.</summary>
    [Fact]
    public void Create_IsPendingWithStableIdAndTrimmedLabel()
    {
        var key = NewKey();
        Assert.Equal(SshKeySyncState.Pending, key.SyncState);
        Assert.NotEqual(Guid.Empty, key.KeyId);
        Assert.Equal("laptop", key.Label);
    }

    /// <summary>A key is bound only to the exact server and case-sensitive account it was created for.</summary>
    /// <param name="serverId">The server asked about.</param>
    /// <param name="account">The account asked about.</param>
    /// <param name="expected">Whether the key should be bound.</param>
    [Theory]
    [InlineData(1, "web", true)]
    [InlineData(2, "web", false)]
    [InlineData(1, "Web", false)]
    [InlineData(null, "web", false)]
    public void IsBoundTo_RequiresSameServerAndAccount(int? serverId, string account, bool expected) =>
        Assert.Equal(expected, NewKey().IsBoundTo(serverId, account));

    /// <summary>A revoked key is inactive and accepts no further state change.</summary>
    [Fact]
    public void Revoked_IsTerminal()
    {
        var key = NewKey();
        key.MarkInstalled();
        var revokedAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        key.MarkRevoked(revokedAt);
        Assert.False(key.IsActive);
        Assert.Equal(revokedAt, key.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => key.MarkInstalled());
        Assert.Throws<InvalidOperationException>(() => key.MarkRevokeFailed(SshProviderFailure.Unreachable));
        Assert.Throws<InvalidOperationException>(() => key.MarkSuspended());
        Assert.Throws<InvalidOperationException>(() => key.RecordSyncError(SshProviderFailure.Timeout));
    }

    /// <summary>A failed revoke keeps the key active and records the error.</summary>
    [Fact]
    public void RevokeFailed_StaysActive()
    {
        var key = NewKey();
        key.MarkInstalled();
        key.MarkRevokeFailed(SshProviderFailure.Unreachable);
        Assert.True(key.IsActive);
        Assert.Equal(SshProviderFailure.Unreachable, key.LastSyncError);
    }

    /// <summary>A failed install records why; a later confirmed install clears it.</summary>
    [Fact]
    public void InstallFailed_RecordsTheFailure_AndInstallClearsIt()
    {
        var key = NewKey();
        key.MarkInstallFailed(SshProviderFailure.Timeout);
        Assert.Equal(SshKeySyncState.InstallFailed, key.SyncState);
        Assert.Equal(SshProviderFailure.Timeout, key.LastSyncError);

        key.MarkInstalled();
        Assert.Null(key.LastSyncError);
    }

    /// <summary>A key taken off for a suspension stays active, clears the last error and can be installed again.</summary>
    [Fact]
    public void Suspended_StaysActive_AndCanBeRestored()
    {
        var key = NewKey();
        key.MarkInstallFailed(SshProviderFailure.Timeout);
        key.MarkSuspended();
        Assert.Equal(SshKeySyncState.Suspended, key.SyncState);
        Assert.True(key.IsActive);
        Assert.Null(key.LastSyncError);

        key.MarkInstalled();
        Assert.Equal(SshKeySyncState.Installed, key.SyncState);
    }

    /// <summary>A failed call recorded without a transition keeps the state and stores why it failed.</summary>
    [Fact]
    public void RecordSyncError_KeepsTheState()
    {
        var key = NewKey();
        key.MarkInstalled();
        key.RecordSyncError(SshProviderFailure.Unreachable);
        Assert.Equal(SshKeySyncState.Installed, key.SyncState);
        Assert.Equal(SshProviderFailure.Unreachable, key.LastSyncError);
    }

    /// <summary>A key the client adds is issued to no plugin, so no plugin may claim it.</summary>
    [Fact]
    public void ClientKey_IsIssuedToNoPlugin()
    {
        var key = NewKey();
        Assert.Null(key.IssuedToPluginId);
        Assert.False(key.IsIssuedTo("deployer"));
    }

    /// <summary>A key issued to a plugin keeps the plugin id and answers only for that exact, case-sensitive id.</summary>
    [Fact]
    public void PluginKey_IsIssuedOnlyToThatPlugin()
    {
        var key = SshKey.Create(10, 1, "web", "deploy", "ssh-ed25519 AAAA", "SHA256:x", "deployer");

        Assert.Equal("deployer", key.IssuedToPluginId);
        Assert.True(key.IsIssuedTo("deployer"));
        Assert.False(key.IsIssuedTo("Deployer"));
        Assert.False(key.IsIssuedTo("other"));
    }

    /// <summary>An issued key starts pending like a client's own key, with its own key id and a trimmed label.</summary>
    [Fact]
    public void PluginKey_IsPendingWithItsOwnKeyId()
    {
        var issued = SshKey.Create(10, 1, "web", "  deploy  ", "ssh-ed25519 AAAA", "SHA256:x", "deployer");

        Assert.Equal(SshKeySyncState.Pending, issued.SyncState);
        Assert.Equal("deploy", issued.Label);
        Assert.NotEqual(Guid.Empty, issued.KeyId);
        Assert.NotEqual(NewKey().KeyId, issued.KeyId);
    }

    /// <summary>The plugin a key was issued to survives install and revoke, and the key stays pinned to its server and account.</summary>
    [Fact]
    public void PluginKey_KeepsItsPluginThroughRevoke_AndStaysPinned()
    {
        var key = SshKey.Create(10, 1, "web", "deploy", "ssh-ed25519 AAAA", "SHA256:x", "deployer");

        Assert.True(key.IsBoundTo(1, "web"));
        Assert.False(key.IsBoundTo(2, "web"));
        key.MarkInstalled();
        key.MarkRevoked(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(key.IsIssuedTo("deployer"));
        Assert.False(key.IsActive);
    }

    /// <summary>A blank account, label, public key or fingerprint is refused.</summary>
    /// <param name="account">The account user name.</param>
    /// <param name="label">The label.</param>
    /// <param name="publicKey">The public key line.</param>
    /// <param name="fingerprint">The fingerprint.</param>
    [Theory]
    [InlineData(" ", "laptop", "ssh-ed25519 AAAA", "SHA256:x")]
    [InlineData("web", " ", "ssh-ed25519 AAAA", "SHA256:x")]
    [InlineData("web", "laptop", "", "SHA256:x")]
    [InlineData("web", "laptop", "ssh-ed25519 AAAA", " ")]
    public void Create_BlankText_Throws(string account, string label, string publicKey, string fingerprint)
    {
        var act = () => SshKey.Create(10, 1, account, label, publicKey, fingerprint, null);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A key can be issued only to a plugin id that meets the plugin id rule; a valid one is kept.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("acme-sites", true)]
    [InlineData("Acme_Sites", false)]
    [InlineData("ui", false)]
    [InlineData("", false)]
    public void Create_IssuedToPluginId_MustMeetThePluginIdRule(string pluginId, bool valid)
    {
        var act = () => SshKey.Create(10, 1, "web", "deploy", "ssh-ed25519 AAAA", "SHA256:x", pluginId);

        if (valid)
        {
            act().IssuedToPluginId.Should().Be(pluginId);
        }
        else
        {
            act.Should().Throw<ArgumentException>();
        }
    }
}
