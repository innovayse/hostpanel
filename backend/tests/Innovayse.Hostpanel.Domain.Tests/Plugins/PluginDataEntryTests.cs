using Innovayse.Hostpanel.Domain.Plugins;

namespace Innovayse.Hostpanel.Domain.Tests.Plugins;

/// <summary>The plugin store's rules: which keys and which values the host accepts, and how a row is created and changed.</summary>
public sealed class PluginDataEntryTests
{
    /// <summary>The longest key, the longest value and its neighbours, used as theory data.</summary>
    /// <returns>The keys that are accepted.</returns>
    public static TheoryData<string> ValidKeys() => new()
    {
        "a",
        new string('a', PluginDataEntry.MaxKeyLength),
        "a.b-c_d:e",
        "A9",
    };

    /// <summary>Keys the store refuses.</summary>
    /// <returns>The keys that are refused.</returns>
    public static TheoryData<string?> InvalidKeys() => new()
    {
        null,
        string.Empty,
        " ",
        new string('a', PluginDataEntry.MaxKeyLength + 1),
        "a b",
        "a/b",
        "a;b",
        "é",
        "a\n",
    };

    /// <summary>Keys of 1 to 200 letters, digits and <c>.-_:</c> are accepted.</summary>
    /// <param name="key">The candidate key.</param>
    [Theory]
    [MemberData(nameof(ValidKeys))]
    public void IsValidKey_AcceptsTheAllowedShape(string key)
    {
        PluginDataEntry.IsValidKey(key).Should().BeTrue();
    }

    /// <summary>Missing, empty, blank, overlong and keys with other characters (a trailing newline included) are refused.</summary>
    /// <param name="key">The candidate key.</param>
    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public void IsValidKey_RefusesEverythingElse(string? key)
    {
        PluginDataEntry.IsValidKey(key).Should().BeFalse();
    }

    /// <summary>An empty text, a short one, one at the cap and one of surrogate pairs are values.</summary>
    /// <param name="value">The candidate value.</param>
    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("\U0001F600 աբգ é")]
    public void IsValidValue_AcceptsText(string value)
    {
        PluginDataEntry.IsValidValue(value).Should().BeTrue();
    }

    /// <summary>A value of exactly <see cref="PluginDataEntry.MaxValueLength"/> characters is accepted, one more is not.</summary>
    [Fact]
    public void IsValidValue_StopsAtTheCap()
    {
        PluginDataEntry.IsValidValue(new string('x', PluginDataEntry.MaxValueLength)).Should().BeTrue();
        PluginDataEntry.IsValidValue(new string('x', PluginDataEntry.MaxValueLength + 1)).Should().BeFalse();
    }

    /// <summary>A missing value is refused.</summary>
    [Fact]
    public void IsValidValue_RefusesNull()
    {
        PluginDataEntry.IsValidValue(null).Should().BeFalse();
    }

    /// <summary>Create keeps the plugin id, the client reference, the key and the value it is given, with no update stamp yet.</summary>
    [Fact]
    public void Create_KeepsWhatItIsGiven()
    {
        var data = PluginDataEntry.Create("reports", "ref-1", "cursor", "42");

        data.PluginId.Should().Be("reports");
        data.ClientRef.Should().Be("ref-1");
        data.Key.Should().Be("cursor");
        data.Value.Should().Be("42");
        data.UpdatedAt.Should().BeNull();
    }

    /// <summary>Create keeps a plugin-wide row's missing client reference and accepts an empty value.</summary>
    [Fact]
    public void Create_AcceptsNoClientReferenceAndAnEmptyValue()
    {
        var data = PluginDataEntry.Create("reports", null, "cursor", string.Empty);

        data.ClientRef.Should().BeNull();
        data.Value.Should().BeEmpty();
    }

    /// <summary>Create refuses a key outside the rules with a PLUGIN_STORE_KEY_INVALID refusal that does not echo the key.</summary>
    /// <param name="key">The refused key.</param>
    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("secret/key")]
    public void Create_WithABadKey_ThrowsWithoutEchoingTheKey(string key)
    {
        var act = () => PluginDataEntry.Create("reports", "ref-1", key, "v");

        var thrown = act.Should().Throw<DomainRuleException>().Which;
        thrown.Code.Should().Be(PluginStoreErrors.KeyInvalid);
        if (key.Length > 0)
        {
            thrown.Message.Should().NotContain(key);
        }
    }

    /// <summary>Create refuses a missing value and one over the cap with a PLUGIN_STORE_VALUE_INVALID refusal that does not echo the value.</summary>
    [Fact]
    public void Create_WithABadValue_ThrowsWithoutEchoingTheValue()
    {
        var oversized = new string('x', PluginDataEntry.MaxValueLength + 1);

        var missing = () => PluginDataEntry.Create("reports", "ref-1", "cursor", null!);
        var big = () => PluginDataEntry.Create("reports", "ref-1", "cursor", oversized);

        missing.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        var thrown = big.Should().Throw<DomainRuleException>().Which;
        thrown.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        thrown.Message.Should().NotContain(oversized);
    }

    /// <summary>ChangeValue replaces the stored text.</summary>
    [Fact]
    public void ChangeValue_ReplacesTheValue()
    {
        var data = PluginDataEntry.Create("reports", "ref-1", "cursor", "old");

        data.ChangeValue("new");

        data.Value.Should().Be("new");
    }

    /// <summary>ChangeValue refuses a missing value and one over the cap with PLUGIN_STORE_VALUE_INVALID, and the old value stays.</summary>
    [Fact]
    public void ChangeValue_WithABadValue_ThrowsAndKeepsTheOldValue()
    {
        var data = PluginDataEntry.Create("reports", "ref-1", "cursor", "old");
        var oversized = new string('x', PluginDataEntry.MaxValueLength + 1);

        var missing = () => data.ChangeValue(null!);
        var big = () => data.ChangeValue(oversized);

        missing.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        var thrown = big.Should().Throw<DomainRuleException>().Which;
        thrown.Code.Should().Be(PluginStoreErrors.ValueInvalid);
        thrown.Message.Should().NotContain(oversized);
        data.Value.Should().Be("old");
    }

    /// <summary>EnsureKey throws a PLUGIN_STORE_KEY_INVALID refusal for a key outside the rules.</summary>
    /// <param name="key">The refused key.</param>
    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public void EnsureKey_WithABadKey_Throws(string? key)
    {
        var act = () => PluginDataEntry.EnsureKey(key);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.KeyInvalid);
    }

    /// <summary>EnsureKey lets a key inside the rules pass.</summary>
    /// <param name="key">The accepted key.</param>
    [Theory]
    [MemberData(nameof(ValidKeys))]
    public void EnsureKey_WithAGoodKey_DoesNotThrow(string key)
    {
        var act = () => PluginDataEntry.EnsureKey(key);

        act.Should().NotThrow();
    }

    /// <summary>EnsureRoomForNewKey lets a scope below the cap take one more key, down to the last free place.</summary>
    /// <param name="keysInScope">How many keys the scope already holds.</param>
    /// <param name="maxKeys">The cap.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(998, 1000)]
    [InlineData(999, 1000)]
    public void EnsureRoomForNewKey_BelowTheCap_DoesNotThrow(int keysInScope, int maxKeys)
    {
        var act = () => PluginDataEntry.EnsureRoomForNewKey(keysInScope, maxKeys);

        act.Should().NotThrow();
    }

    /// <summary>EnsureRoomForNewKey refuses a new key in a scope that is at the cap or over it, with PLUGIN_STORE_KEY_LIMIT_REACHED.</summary>
    /// <param name="keysInScope">How many keys the scope already holds.</param>
    /// <param name="maxKeys">The cap.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1000, 1000)]
    [InlineData(1001, 1000)]
    public void EnsureRoomForNewKey_AtOrAboveTheCap_Throws(int keysInScope, int maxKeys)
    {
        var act = () => PluginDataEntry.EnsureRoomForNewKey(keysInScope, maxKeys);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(PluginStoreErrors.KeyLimitReached);
    }
}
