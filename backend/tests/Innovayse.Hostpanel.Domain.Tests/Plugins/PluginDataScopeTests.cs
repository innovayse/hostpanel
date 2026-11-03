using Innovayse.Hostpanel.Domain.Plugins;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>A plugin store scope's totals: what a write may add, what a removal frees, and that every write moves the revision.</summary>
public sealed class PluginDataScopeTests
{
    /// <summary>The key cap of the quota the tests use.</summary>
    private const int MaxKeys = 2;

    /// <summary>The character cap of the quota the tests use.</summary>
    private const long MaxChars = 100;

    /// <summary>The quota the tests use: two keys, a hundred characters.</summary>
    private static readonly PluginStoreQuota Quota = new(MaxKeys, MaxChars);

    /// <summary>Open starts an empty scope for the plugin and client reference it is given, the plugin-wide one included.</summary>
    [Fact]
    public void Open_StartsEmpty()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        var pluginWide = PluginDataScope.Open("reports", null);

        scope.PluginId.Should().Be("reports");
        scope.ClientRef.Should().Be("ref-1");
        scope.KeyCount.Should().Be(0);
        scope.StoredChars.Should().Be(0);
        scope.Revision.Should().Be(0);
        pluginWide.ClientRef.Should().BeNull();
    }

    /// <summary>New keys within both caps are admitted up to the last free key and the last free character.</summary>
    [Fact]
    public void Admit_WithinTheQuota_AddsToTheTotals()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");

        scope.Admit(1, 40, Quota);
        scope.Admit(1, 60, Quota);

        scope.KeyCount.Should().Be(MaxKeys);
        scope.StoredChars.Should().Be(MaxChars);
    }

    /// <summary>A new key in a scope that holds the most keys is refused with PLUGIN_STORE_QUOTA_EXCEEDED and nothing changes.</summary>
    [Fact]
    public void Admit_OverTheKeyCap_ThrowsAndChangesNothing()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, 1, Quota);
        scope.Admit(1, 1, Quota);

        var act = () => scope.Admit(1, 1, Quota);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        scope.KeyCount.Should().Be(MaxKeys);
        scope.StoredChars.Should().Be(2);
        scope.Revision.Should().Be(2);
    }

    /// <summary>A write that would take the characters one past the cap is refused, for a new key and for a longer value alike.</summary>
    [Fact]
    public void Admit_OverTheCharCap_ThrowsAndChangesNothing()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, MaxChars - 1, Quota);

        var newKey = () => scope.Admit(1, 2, Quota);
        var longerValue = () => scope.Admit(0, 2, Quota);

        newKey.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        longerValue.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.QuotaExceeded);
        scope.KeyCount.Should().Be(1);
        scope.StoredChars.Should().Be(MaxChars - 1);
        scope.Revision.Should().Be(1);
    }

    /// <summary>
    /// In a scope already over a lowered quota, a write that shrinks or keeps its size is admitted, while one that grows
    /// a measure over its cap is not.
    /// </summary>
    [Fact]
    public void Admit_InAScopeOverALoweredQuota_AllowsOnlyWhatDoesNotGrow()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, 80, Quota);
        scope.Admit(1, 20, Quota);
        var lowered = new PluginStoreQuota(1, 10);

        scope.CanAdmit(0, -5, lowered).Should().BeTrue();
        scope.CanAdmit(0, 0, lowered).Should().BeTrue();
        scope.CanAdmit(0, 1, lowered).Should().BeFalse();
        scope.CanAdmit(1, -50, lowered).Should().BeFalse();

        scope.Admit(0, -5, lowered);
        scope.Admit(0, 0, lowered);

        scope.KeyCount.Should().Be(MaxKeys);
        scope.StoredChars.Should().Be(95);
    }

    /// <summary>CanAdmit answers what Admit would do, at the edge of both caps.</summary>
    [Fact]
    public void CanAdmit_MatchesTheCaps()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, 50, Quota);

        scope.CanAdmit(1, 50, Quota).Should().BeTrue();
        scope.CanAdmit(1, 51, Quota).Should().BeFalse();
        scope.CanAdmit(0, 50, Quota).Should().BeTrue();
    }

    /// <summary>Release takes one key and its characters out of the totals, making room for a new key.</summary>
    [Fact]
    public void Release_FreesTheKeyAndItsCharacters()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, 30, Quota);
        scope.Admit(1, 70, Quota);

        scope.Release(70);

        scope.KeyCount.Should().Be(1);
        scope.StoredChars.Should().Be(30);
        scope.CanAdmit(1, 70, Quota).Should().BeTrue();
    }

    /// <summary>Every Admit and every Release moves the revision by one, a write that changes no total included.</summary>
    [Fact]
    public void EveryWrite_MovesTheRevision()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");

        scope.Admit(1, 10, Quota);
        scope.Admit(0, 0, Quota);
        scope.Admit(0, -3, Quota);
        scope.Release(7);

        scope.Revision.Should().Be(4);
    }

    /// <summary>The totals never go negative: a release with no key, or of more characters than held, throws and changes nothing.</summary>
    [Fact]
    public void Release_MoreThanHeld_ThrowsAndChangesNothing()
    {
        var empty = PluginDataScope.Open("reports", "ref-1");
        var scope = PluginDataScope.Open("reports", "ref-2");
        scope.Admit(1, 10, Quota);

        var noKey = () => empty.Release(0);
        var tooMany = () => scope.Release(11);
        var negative = () => scope.Release(-1);

        noKey.Should().Throw<ArgumentOutOfRangeException>();
        tooMany.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        empty.KeyCount.Should().Be(0);
        empty.Revision.Should().Be(0);
        scope.KeyCount.Should().Be(1);
        scope.StoredChars.Should().Be(10);
        scope.Revision.Should().Be(1);
    }

    /// <summary>A write cannot free more characters than the scope holds, and a negative key count is refused.</summary>
    [Fact]
    public void Admit_BelowZero_Throws()
    {
        var scope = PluginDataScope.Open("reports", "ref-1");
        scope.Admit(1, 10, Quota);

        var freesTooMuch = () => scope.Admit(0, -11, Quota);
        var negativeKeys = () => scope.Admit(-1, 0, Quota);

        freesTooMuch.Should().Throw<ArgumentOutOfRangeException>();
        negativeKeys.Should().Throw<ArgumentOutOfRangeException>();
        scope.StoredChars.Should().Be(10);
        scope.KeyCount.Should().Be(1);
        scope.Revision.Should().Be(1);
    }
}
