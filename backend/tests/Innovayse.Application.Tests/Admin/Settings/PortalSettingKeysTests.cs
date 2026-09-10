namespace Innovayse.Application.Tests.Admin.Settings;

using Innovayse.Domain.Settings;
using Xunit;

/// <summary>
/// Tests for <see cref="PortalSettingKeys"/>: the invariants that keep the seed list, the
/// public allow-list and the value vocabularies from drifting apart. Nothing else would notice
/// — a key seeded but not public just renders the build-time default, silently.
/// </summary>
public class PortalSettingKeysTests
{
    /// <summary>Every seeded key is public and every public key is seeded.</summary>
    [Fact]
    public void Public_MatchesDefaults_Exactly()
    {
        var seeded = PortalSettingKeys.Defaults.Select(d => d.Key).ToHashSet();

        Assert.Equal(seeded, PortalSettingKeys.Public);
    }

    /// <summary>A seeded key appears once; a duplicate would make the seeder insert it twice.</summary>
    [Fact]
    public void Defaults_HaveNoDuplicateKeys()
    {
        var keys = PortalSettingKeys.Defaults.Select(d => d.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>Every key with a vocabulary is a seeded, public key.</summary>
    [Fact]
    public void AllowedValues_OnlyNameSeededKeys()
    {
        foreach (var key in PortalSettingKeys.AllowedValues.Keys)
        {
            Assert.Contains(key, PortalSettingKeys.Public);
        }
    }

    /// <summary>The value a fresh install starts from must itself pass the vocabulary check.</summary>
    [Fact]
    public void Defaults_ForVocabularyKeys_AreAllowed()
    {
        foreach (var (key, value, _) in PortalSettingKeys.Defaults)
        {
            if (PortalSettingKeys.AllowedValues.TryGetValue(key, out var allowed))
            {
                Assert.Contains(value, allowed);
            }
        }
    }

    /// <summary>The template vocabulary matches what the storefront's registry can render.</summary>
    [Fact]
    public void TemplateNames_MatchTheClientRegistry()
    {
        Assert.Equal(
            new[] { "aurora", "classic", "nova" },
            PortalSettingKeys.TemplateNames.OrderBy(n => n).ToArray());
    }
}
